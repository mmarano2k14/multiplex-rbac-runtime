using Multiplexed.Abstractions.AI.Concurrency;
using StackExchange.Redis;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.AI.Concurrency
{
    /// <summary>
    /// Redis-backed distributed concurrency gate for AI runtime step execution.
    /// </summary>
    /// <remarks>
    /// This gate enforces distributed concurrency limits through Redis Lua scripts.
    ///
    /// Supported scopes:
    /// - global runtime concurrency
    /// - pipeline concurrency
    /// - step concurrency
    /// - execution concurrency
    /// - runtime instance concurrency
    ///
    /// The gate is responsible only for acquiring and releasing distributed execution capacity.
    /// It does not decide policy eligibility and does not claim DAG steps.
    ///
    /// Crash recovery is handled through a Redis lease key with TTL.
    /// Re-acquiring the same lease is idempotent.
    /// </remarks>
    public sealed class RedisAiConcurrencyGate : IAiConcurrencyGate
    {
        private static readonly LuaScript AcquirePreparedScript = LuaScript.Prepare(
            """
            local keys = cjson.decode(@keysJson)
            local limits = cjson.decode(@limitsJson)
            local leaseKey = @leaseKey
            local leaseTtlSeconds = tonumber(@leaseTtlSeconds)

            if redis.call('EXISTS', leaseKey) == 1 then
                return 1
            end

            local acquired = {}

            for i, key in ipairs(keys) do
                local limit = tonumber(limits[i])

                if limit ~= nil and limit > 0 then
                    local current = redis.call('INCR', key)

                    if current > limit then
                        redis.call('DECR', key)

                        for _, acquiredKey in ipairs(acquired) do
                            local value = redis.call('DECR', acquiredKey)

                            if value < 0 then
                                redis.call('SET', acquiredKey, 0)
                            end
                        end

                        return 0
                    end

                    table.insert(acquired, key)
                end
            end

            redis.call('SET', leaseKey, cjson.encode(acquired), 'EX', leaseTtlSeconds)

            return 1
            """);

        private static readonly LuaScript ReleasePreparedScript = LuaScript.Prepare(
            """
            local leaseKey = @leaseKey

            local raw = redis.call('GET', leaseKey)
            if not raw then
                return 1
            end

            local keys = cjson.decode(raw)

            redis.call('DEL', leaseKey)

            for _, key in ipairs(keys) do
                local current = redis.call('DECR', key)

                if current < 0 then
                    redis.call('SET', key, 0)
                end
            end

            return 1
            """);

        private readonly IConnectionMultiplexer _multiplexer;
        private readonly IDatabase _database;

        private LoadedLuaScript _acquireLoadedScript;
        private LoadedLuaScript _releaseLoadedScript;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisAiConcurrencyGate"/> class.
        /// </summary>
        /// <param name="multiplexer">The Redis connection multiplexer.</param>
        public RedisAiConcurrencyGate(IConnectionMultiplexer multiplexer)
        {
            ArgumentNullException.ThrowIfNull(multiplexer);

            _multiplexer = multiplexer;
            _database = multiplexer.GetDatabase();

            _acquireLoadedScript = LoadScript(AcquirePreparedScript);
            _releaseLoadedScript = LoadScript(ReleasePreparedScript);
        }

        /// <inheritdoc />
        public async Task<AiConcurrencyDecision> TryAcquireAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(definition);

            if (!definition.Enabled)
            {
                return AiConcurrencyDecision.Allow();
            }

            var scopes = BuildScopes(context, definition);

            if (scopes.Count == 0)
            {
                return AiConcurrencyDecision.Allow();
            }

            var keysJson = JsonSerializer.Serialize(scopes.Select(scope => scope.Key));
            var limitsJson = JsonSerializer.Serialize(scopes.Select(scope => scope.Limit));
            var leaseKey = GetLeaseKey(context);
            var leaseTtlSeconds = Math.Max(1, definition.LeaseSeconds);

            try
            {
                return await TryAcquireAsync(
                        keysJson,
                        limitsJson,
                        leaseKey,
                        leaseTtlSeconds,
                        definition)
                    .ConfigureAwait(false);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _acquireLoadedScript = LoadScript(AcquirePreparedScript);

                return await TryAcquireAsync(
                        keysJson,
                        limitsJson,
                        leaseKey,
                        leaseTtlSeconds,
                        definition)
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(definition);

            if (!definition.Enabled)
            {
                return;
            }

            var scopes = BuildScopes(context, definition);

            if (scopes.Count == 0)
            {
                return;
            }

            var leaseKey = GetLeaseKey(context);

            try
            {
                await ReleaseAsync(leaseKey).ConfigureAwait(false);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _releaseLoadedScript = LoadScript(ReleasePreparedScript);

                await ReleaseAsync(leaseKey).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Attempts to acquire all configured concurrency scopes atomically.
        /// </summary>
        /// <param name="keysJson">The serialized Redis counter keys.</param>
        /// <param name="limitsJson">The serialized concurrency limits.</param>
        /// <param name="leaseKey">The Redis lease key used for idempotency and crash recovery.</param>
        /// <param name="leaseTtlSeconds">The lease TTL in seconds.</param>
        /// <param name="definition">The concurrency definition.</param>
        /// <returns>The concurrency decision.</returns>
        private async Task<AiConcurrencyDecision> TryAcquireAsync(
            string keysJson,
            string limitsJson,
            string leaseKey,
            int leaseTtlSeconds,
            AiConcurrencyDefinition definition)
        {
            var result = await _acquireLoadedScript.EvaluateAsync(
                _database,
                new
                {
                    keysJson = (RedisValue)keysJson,
                    limitsJson = (RedisValue)limitsJson,
                    leaseKey = (RedisKey)leaseKey,
                    leaseTtlSeconds = (RedisValue)leaseTtlSeconds
                });

            var allowed = (int)result! == 1;

            return allowed
                ? AiConcurrencyDecision.Allow()
                : AiConcurrencyDecision.Deny(
                    "Concurrency limit reached.",
                    TimeSpan.FromMilliseconds(definition.DefaultRetryAfterMs));
        }

        /// <summary>
        /// Releases all concurrency scopes associated with the lease.
        /// </summary>
        /// <param name="leaseKey">The Redis lease key used to resolve acquired counters.</param>
        private async Task ReleaseAsync(string leaseKey)
        {
            await _releaseLoadedScript.EvaluateAsync(
                _database,
                new
                {
                    leaseKey = (RedisKey)leaseKey
                });
        }

        /// <summary>
        /// Builds the Redis concurrency scopes enabled by the concurrency definition.
        /// </summary>
        /// <param name="context">The concurrency context.</param>
        /// <param name="definition">The concurrency definition.</param>
        /// <returns>The enabled concurrency scopes.</returns>
        private static List<ConcurrencyScope> BuildScopes(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition)
        {
            var scopes = new List<ConcurrencyScope>();

            AddScope(
                scopes,
                "ai:concurrency:global",
                definition.MaxGlobalConcurrency);

            AddScope(
                scopes,
                $"ai:concurrency:pipeline:{context.PipelineKey}",
                definition.MaxPipelineConcurrency);

            AddScope(
                scopes,
                $"ai:concurrency:step:{context.StepKey}",
                definition.MaxStepConcurrency);

            AddScope(
                scopes,
                $"ai:concurrency:execution:{context.ExecutionId}",
                definition.MaxExecutionConcurrency);

            AddScope(
                scopes,
                $"ai:concurrency:instance:{context.RuntimeInstanceId}",
                definition.MaxInstanceConcurrency);

            return scopes;
        }

        /// <summary>
        /// Adds a concurrency scope when its limit is configured.
        /// </summary>
        /// <param name="scopes">The target scope collection.</param>
        /// <param name="key">The Redis counter key.</param>
        /// <param name="limit">The configured limit.</param>
        private static void AddScope(
            ICollection<ConcurrencyScope> scopes,
            string key,
            int? limit)
        {
            if (!limit.HasValue || limit.Value <= 0)
            {
                return;
            }

            scopes.Add(new ConcurrencyScope(key, limit.Value));
        }

        /// <summary>
        /// Builds the Redis lease key for a concurrency acquisition.
        /// </summary>
        /// <param name="context">The concurrency context.</param>
        /// <returns>The Redis lease key.</returns>
        private static string GetLeaseKey(AiConcurrencyContext context)
        {
            return $"ai:concurrency:lease:{context.LeaseId}";
        }

        /// <summary>
        /// Retrieves a connected Redis server for script loading.
        /// </summary>
        /// <returns>The connected Redis server.</returns>
        private IServer GetServer()
        {
            return _multiplexer.GetEndPoints()
                .Select(endpoint => _multiplexer.GetServer(endpoint))
                .First(server => server.IsConnected);
        }

        /// <summary>
        /// Loads a prepared Lua script onto Redis and returns its SHA-bound instance.
        /// </summary>
        /// <param name="script">The prepared Lua script.</param>
        /// <returns>The loaded Lua script.</returns>
        private LoadedLuaScript LoadScript(LuaScript script)
        {
            var server = GetServer();
            return script.Load(server);
        }

        /// <summary>
        /// Represents one configured Redis concurrency scope.
        /// </summary>
        /// <param name="Key">The Redis counter key.</param>
        /// <param name="Limit">The concurrency limit.</param>
        private sealed record ConcurrencyScope(string Key, int Limit);
    }
}