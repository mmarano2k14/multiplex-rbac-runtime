using Microsoft.Extensions.Options;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Runtime;
using StackExchange.Redis;
using System.Text.Json;

namespace MultiplexedRbac.Stores.Cache
{
    public sealed class RedisContextStore : IContextStore
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly IDatabase _db;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ContextRuntimeOptions _options;

        private const string ContextPrefix = "ac:ctx:";
        private const string InFlightPrefix = "ac:inflight:";

        private const int DefaultTtlSeconds = 900;  // 15 minutes
        private const int InFlightExtraSeconds = 5; // safety margin

        // SHA cache kept in memory by the application process
        private string? _acquireScriptSha;
        private string? _releaseScriptSha;
        private string? _rotateScriptSha;

        public RedisContextStore(
            IConnectionMultiplexer multiplexer,
            IOptions<ContextRuntimeOptions> options)
        {
            _multiplexer = multiplexer;
            _db = multiplexer.GetDatabase();
            _options = options.Value;
            _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        }

        private string ContextKey(string key) => $"{ContextPrefix}{key}";
        private string InFlightKey(string key) => $"{InFlightPrefix}{key}";

        private static string GenerateNewKey()
            => "ctx_" + Guid.NewGuid().ToString("N");

        private const string AcquireScript = @"
            local ctxKey = KEYS[1]
            local inKey = KEYS[2]

            local extra = tonumber(ARGV[1])
            local defaultTtl = tonumber(ARGV[2])
            local maxInFlight = tonumber(ARGV[3])

            if redis.call('EXISTS', ctxKey) == 0 then
              return 0
            end

            local ttl = redis.call('TTL', ctxKey)
            if ttl < 0 then
              ttl = defaultTtl
            end

            local n = redis.call('INCR', inKey)

            if maxInFlight > 0 and n > maxInFlight then
                redis.call('DECR', inKey)
                return -1
            end

            redis.call('EXPIRE', inKey, ttl + extra)
            return n
        ";

        private const string ReleaseScript = @"
            local inKey = KEYS[1]

            if redis.call('EXISTS', inKey) == 0 then
                return 0
            end

            local n = redis.call('DECR', inKey)

            if n <= 0 then
                redis.call('DEL', inKey)
            end

            return n
        ";

        /// <summary>
        /// Rotation script using millisecond overlap control.
        ///
        /// - new key gets the full normal TTL (seconds)
        /// - old key is shortened to overlap window (milliseconds)
        /// - if overlap is 0, the old key expires immediately
        /// </summary>
        private const string RotateScript = @"
            local oldKey = KEYS[1]
            local newKey = KEYS[2]
            local inKey = KEYS[3]

            local newTtlSeconds = tonumber(ARGV[1])
            local overlapMs = tonumber(ARGV[2])
            local inFlightGraceMs = tonumber(ARGV[3])

            local value = redis.call('GET', oldKey)
            if not value then
              return nil
            end

            redis.call('SET', newKey, value, 'EX', newTtlSeconds)

            local inFlight = tonumber(redis.call('GET', inKey) or '0')

            local effectiveOverlapMs = overlapMs
            if inFlight > 0 and inFlightGraceMs > effectiveOverlapMs then
              effectiveOverlapMs = inFlightGraceMs
            end

            local oldPttl = redis.call('PTTL', oldKey)
            if oldPttl < 0 then
              redis.call('PEXPIRE', oldKey, effectiveOverlapMs)
            elseif oldPttl > effectiveOverlapMs then
              redis.call('PEXPIRE', oldKey, effectiveOverlapMs)
            end

            return value
        ";

        // ------------------------------------------------------------
        // Generic script execution helper
        // ------------------------------------------------------------
        private async Task<RedisResult> EvaluateScriptAsync(
            string script,
            RedisKey[] keys,
            RedisValue[] values,
            Func<string?> getSha,
            Action<string> setSha)
        {
            if (!_options.UseRedisLuaScriptShaCaching)
            {
                return await _db.ScriptEvaluateAsync(script, keys, values);
            }

            var sha = getSha();

            try
            {
                if (!string.IsNullOrWhiteSpace(sha))
                {
                    return await _db.ScriptEvaluateAsync(sha, keys, values);
                }

                sha = await LoadScriptShaAsync(script);
                setSha(sha);

                return await _db.ScriptEvaluateAsync(sha, keys, values);
            }
            catch (RedisServerException ex) when (ex.Message.StartsWith("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                sha = await LoadScriptShaAsync(script);
                setSha(sha);

                return await _db.ScriptEvaluateAsync(sha, keys, values);
            }
        }

        private async Task<string> LoadScriptShaAsync(string script)
        {
            var endpoint = _multiplexer.GetEndPoints().FirstOrDefault()
                ?? throw new InvalidOperationException("No Redis endpoints available.");

            var server = _multiplexer.GetServer(endpoint);

            // SCRIPT LOAD returns SHA1 (byte[])
            var result = await server.ScriptLoadAsync(script);

            return Convert.ToHexString(result).ToLowerInvariant();
        }

        // ------------------------------------------------------------
        // Store
        // ------------------------------------------------------------
        public async Task<string> StoreAsync(Core.ExecutionContext.ExecutionContext context)
        {
            var key = GenerateNewKey();
            context.ContextKey = key;
            var json = JsonSerializer.Serialize(context, _jsonOptions);

            await _db.StringSetAsync(
                ContextKey(key),
                json,
                TimeSpan.FromSeconds(DefaultTtlSeconds));

            return key;
        }

        // ------------------------------------------------------------
        // Seed just in case forcing context with no dynamic key
        // ------------------------------------------------------------
        public async Task<string> SeedAsync(Core.ExecutionContext.ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(context.ContextKey))
                throw new ArgumentException("ContextKey must be set for seeding.", nameof(context));

            var key = context.ContextKey;
            var json = JsonSerializer.Serialize(context, _jsonOptions);

            await _db.StringSetAsync(
                ContextKey(key),
                json,
                TimeSpan.FromSeconds(DefaultTtlSeconds));

            return key;
        }

        // ------------------------------------------------------------
        // Get
        // ------------------------------------------------------------
        public async Task<Core.ExecutionContext.ExecutionContext?> GetAsync(string key)
        {
            var value = await _db.StringGetAsync(ContextKey(key));
            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<Core.ExecutionContext.ExecutionContext>(value!, _jsonOptions);
        }

        // ------------------------------------------------------------
        // Acquire In-Flight (Lua atomic)
        // ------------------------------------------------------------
        public async Task<bool> TryAcquireInFlightAsync(string key, int maxInFlight)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            // unlimited demo mode
            if (maxInFlight <= 0)
                return true;

            var rr = await EvaluateScriptAsync(
                AcquireScript,
                new RedisKey[] { ContextKey(key), InFlightKey(key) },
                new RedisValue[]
                {
                    (RedisValue)InFlightExtraSeconds,
                    (RedisValue)DefaultTtlSeconds,
                    (RedisValue)maxInFlight
                },
                () => _acquireScriptSha,
                sha => _acquireScriptSha = sha);

            var n = (long)rr;
            return n > 0;
        }

        // ------------------------------------------------------------
        // Release In-Flight (Lua atomic)
        // ------------------------------------------------------------
        public async Task ReleaseInFlightAsync(string key)
        {
            await EvaluateScriptAsync(
                ReleaseScript,
                new RedisKey[] { InFlightKey(key) },
                Array.Empty<RedisValue>(),
                () => _releaseScriptSha,
                sha => _releaseScriptSha = sha);
        }

        // ------------------------------------------------------------
        // Rotation (Lua overlap-safe, overlap in milliseconds)
        // ------------------------------------------------------------
        public async Task<(string newKey, Core.ExecutionContext.ExecutionContext context)> RotateAsync(
            string key,
            TimeSpan overlapWindow)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null/empty.", nameof(key));

            var newKey = GenerateNewKey();

            var overlapMs = Math.Max(
                0L,
                (long)Math.Ceiling(overlapWindow.TotalMilliseconds));

            // Small safety floor to avoid old key disappearing too early
            // while requests already acquired on that key are still finishing.
            var inFlightGraceMs = Math.Max(
                0L,
                InFlightExtraSeconds * 1000L);

            inFlightGraceMs = 0;

            var result = await EvaluateScriptAsync(
                RotateScript,
                new RedisKey[]
                {
                    ContextKey(key),
                    ContextKey(newKey),
                    InFlightKey(key)
                },
                new RedisValue[]
                {
            DefaultTtlSeconds,
            overlapMs,
            inFlightGraceMs
                },
                () => _rotateScriptSha,
                sha => _rotateScriptSha = sha);

            if (result.IsNull)
                throw new KeyNotFoundException("Cannot rotate: context not found or expired.");

            var json = (string)result!;
            var ctx = JsonSerializer.Deserialize<Core.ExecutionContext.ExecutionContext>(json, _jsonOptions)
                      ?? throw new InvalidOperationException("Invalid context JSON in Redis.");

            ctx.ContextKey = key;

            return (newKey, ctx);
        }
    }
}