using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution;
using StackExchange.Redis;

namespace Multiplexed.AI.Stores.Cache
{
    /// <summary>
    /// Redis-backed implementation of <see cref="IAiExecutionStore"/>.
    ///
    /// This store provides:
    /// - Primary persistence for execution records and states
    /// - Atomic compare-and-swap updates using Redis Lua scripting
    ///
    /// Key design goals:
    /// - Ensure consistency under concurrency
    /// - Prevent multiple workers from advancing the same execution step
    /// - Keep record/state updates strictly aligned
    /// </summary>
    public sealed class RedisAiExecutionStore : IAiExecutionStore
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly IDatabase _database;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Prepared Lua script using named parameters.
        ///
        /// IMPORTANT:
        /// - Uses @param syntax (NOT KEYS/ARGV)
        /// - Compatible with StackExchange.Redis LuaScript.Prepare
        /// </summary>
        private static readonly LuaScript TryUpdatePreparedScript = LuaScript.Prepare(
            """
            local currentRecordJson = redis.call('GET', @recordKey)
            if not currentRecordJson then
                return 0
            end

            local currentRecord = cjson.decode(currentRecordJson)
            if not currentRecord then
                return 0
            end

            -- Optimistic concurrency check
            if currentRecord.ExecutionStepKey ~= @expectedStepKey then
                return 0
            end

            -- Apply update atomically
            redis.call('SET', @recordKey, @newRecordJson)
            redis.call('SET', @stateKey, @newStateJson)

            return 1
            """);

        /// <summary>
        /// Loaded script bound to Redis server (executed via SHA).
        /// </summary>
        private LoadedLuaScript _tryUpdateLoadedScript;

        /// <summary>
        /// Initializes a new instance of the Redis execution store.
        /// </summary>
        public RedisAiExecutionStore(IConnectionMultiplexer multiplexer)
        {
            ArgumentNullException.ThrowIfNull(multiplexer);

            _multiplexer = multiplexer;
            _database = multiplexer.GetDatabase();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            _tryUpdateLoadedScript = LoadTryUpdateScript();
        }

        /// <summary>
        /// Creates a new execution record and state.
        ///
        /// This operation overwrites existing values.
        /// Intended for initialization only.
        /// </summary>
        public async Task CreateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (!string.Equals(record.ExecutionId, state.ExecutionId, StringComparison.Ordinal))
                throw new ArgumentException("Record and State must share the same ExecutionId.");

            var recordKey = GetRecordRedisKey(record.ExecutionId);
            var stateKey = GetStateRedisKey(state.ExecutionId);

            var recordPayload = JsonSerializer.Serialize(record, _jsonOptions);
            var statePayload = JsonSerializer.Serialize(state, _jsonOptions);

            await _database.StringSetAsync(recordKey, recordPayload);
            await _database.StringSetAsync(stateKey, statePayload);
        }

        /// <summary>
        /// Retrieves an execution record.
        /// </summary>
        public async Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var value = await _database.StringGetAsync(GetRecordRedisKey(executionId));

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<AiExecutionRecord>((string)value!, _jsonOptions);
        }

        /// <summary>
        /// Retrieves an execution state.
        /// </summary>
        public async Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var value = await _database.StringGetAsync(GetStateRedisKey(executionId));

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<AiExecutionState>((string)value!, _jsonOptions);
        }

        /// <summary>
        /// Attempts to update the execution record and state atomically.
        ///
        /// Uses optimistic concurrency with ExecutionStepKey:
        /// - Only one caller can update for a given step key
        /// - Prevents duplicate execution
        /// </summary>
        public async Task<bool> TryUpdateAsync(
            string executionId,
            string expectedStepKey,
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            if (string.IsNullOrWhiteSpace(expectedStepKey))
                throw new ArgumentException("Expected step key cannot be null or empty.", nameof(expectedStepKey));

            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            var recordKey = GetRecordRedisKey(executionId);
            var stateKey = GetStateRedisKey(executionId);

            var recordPayload = JsonSerializer.Serialize(record, _jsonOptions);
            var statePayload = JsonSerializer.Serialize(state, _jsonOptions);

            try
            {
                var result = await _database.ScriptEvaluateAsync(
                    _tryUpdateLoadedScript,
                    new
                    {
                        recordKey = (RedisKey)recordKey,
                        stateKey = (RedisKey)stateKey,
                        expectedStepKey = (RedisValue)expectedStepKey,
                        newRecordJson = (RedisValue)recordPayload,
                        newStateJson = (RedisValue)statePayload
                    });

                return (int)result! == 1;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                // Redis lost script cache → reload + retry
                _tryUpdateLoadedScript = LoadTryUpdateScript();

                var result = await _database.ScriptEvaluateAsync(
                    _tryUpdateLoadedScript,
                    new
                    {
                        recordKey = (RedisKey)recordKey,
                        stateKey = (RedisKey)stateKey,
                        expectedStepKey = (RedisValue)expectedStepKey,
                        newRecordJson = (RedisValue)recordPayload,
                        newStateJson = (RedisValue)statePayload
                    });

                return (int)result! == 1;
            }
        }

        /// <summary>
        /// Loads the Lua script into Redis and returns the SHA-bound instance.
        /// </summary>
        private LoadedLuaScript LoadTryUpdateScript()
        {
            var endpoint = _multiplexer.GetEndPoints().First();
            var server = _multiplexer.GetServer(endpoint);

            return TryUpdatePreparedScript.Load(server);
        }

        private static string GetRecordRedisKey(string executionId)
            => $"ai:execution:record:{executionId}";

        private static string GetStateRedisKey(string executionId)
            => $"ai:execution:state:{executionId}";
    }
}