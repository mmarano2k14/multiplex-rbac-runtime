using System.Text.Json;
using Multiplexed.AI.Runtime.Execution;
using StackExchange.Redis;

namespace Multiplexed.AI.Stores.Cache
{
    /// <summary>
    /// Redis-backed primary store for AI execution records and states.
    /// </summary>
    public sealed class RedisAiExecutionStore : IAiExecutionStore
    {
        private readonly IDatabase _database;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisAiExecutionStore(IConnectionMultiplexer multiplexer)
        {
            ArgumentNullException.ThrowIfNull(multiplexer);

            _database = multiplexer.GetDatabase();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Creates a new execution record and state in Redis.
        /// </summary>
        public async Task CreateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            var recordKey = GetRecordRedisKey(record.ExecutionId);
            var stateKey = GetStateRedisKey(state.ExecutionId);

            var recordPayload = JsonSerializer.Serialize(record, _jsonOptions);
            var statePayload = JsonSerializer.Serialize(state, _jsonOptions);

            await _database.StringSetAsync(recordKey, recordPayload);
            await _database.StringSetAsync(stateKey, statePayload);
        }

        /// <summary>
        /// Retrieves an execution record from Redis.
        /// </summary>
        public async Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var key = GetRecordRedisKey(executionId);
            var value = await _database.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            var json = (string)value!;

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<AiExecutionRecord>(json, _jsonOptions);
        }

        /// <summary>
        /// Retrieves an execution state from Redis.
        /// </summary>
        public async Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var key = GetStateRedisKey(executionId);
            var value = await _database.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            var json = (string)value!;

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<AiExecutionState>(json, _jsonOptions);
        }

        /// <summary>
        /// Attempts to update an execution record and state using optimistic concurrency.
        /// 
        /// This initial version performs a read-check-write sequence.
        /// A stricter atomic Redis/Lua implementation can be introduced later.
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

            var currentRecord = await GetRecordAsync(executionId, cancellationToken);

            if (currentRecord is null)
                return false;

            if (!string.Equals(currentRecord.ExecutionStepKey, expectedStepKey, StringComparison.Ordinal))
                return false;

            await CreateAsync(record, state, cancellationToken);
            return true;
        }

        /// <summary>
        /// Builds the Redis key used for execution records.
        /// </summary>
        private static string GetRecordRedisKey(string executionId)
        {
            return $"ai:execution:record:{executionId}";
        }

        /// <summary>
        /// Builds the Redis key used for execution state.
        /// </summary>
        private static string GetStateRedisKey(string executionId)
        {
            return $"ai:execution:state:{executionId}";
        }
    }
}