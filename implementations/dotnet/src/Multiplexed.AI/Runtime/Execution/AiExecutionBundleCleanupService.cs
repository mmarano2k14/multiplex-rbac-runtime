using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Stores;
using StackExchange.Redis;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Deletes all Redis resources owned by a single AI execution.
    ///
    /// This cleanup treats an AI execution as a bundle composed of:
    /// - the AI execution record
    /// - the AI execution state
    /// - the AI-owned RBAC context copy
    /// - the AI-owned RBAC in-flight tracking key
    ///
    /// IMPORTANT:
    /// - This service does not participate in execution progression
    /// - This service is intentionally separate from IAiExecutionStore
    /// - Stable key naming conventions are used to reconstruct owned resources
    /// - The record is read first so the AI-owned ContextKey can be recovered
    /// </summary>
    public sealed class AiExecutionBundleCleanupService
    {
        private readonly IAiExecutionStore _store;
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly IDatabase _database;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionBundleCleanupService"/> class.
        /// </summary>
        public AiExecutionBundleCleanupService(
            IAiExecutionStore store,
            IConnectionMultiplexer multiplexer)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(multiplexer);

            _store = store;
            _multiplexer = multiplexer;
            _database = multiplexer.GetDatabase();
        }

        /// <summary>
        /// Deletes the full resource bundle associated with the specified execution.
        ///
        /// This method:
        /// - loads the persisted execution record
        /// - reconstructs all known Redis keys owned by the execution
        /// - deletes AI keys and AI-owned RBAC keys together
        ///
        /// Notes:
        /// - Returns false only when nothing was deleted
        /// - Still attempts AI key cleanup even if the record cannot be loaded
        /// - Context cleanup requires the persisted ContextKey from the record
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if at least one Redis key was deleted; otherwise false.</returns>
        public async Task<bool> DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var keys = new HashSet<string>(StringComparer.Ordinal)
            {
                GetExecutionRecordKey(executionId),
                GetExecutionStateKey(executionId)
            };

            var record = await _store.GetRecordAsync(executionId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(record?.ContextKey))
            {
                keys.Add(GetRbacContextKey(record.ContextKey));
                keys.Add(GetRbacInFlightKey(record.ContextKey));
            }

            var redisKeys = keys
                .Select(x => (RedisKey)x)
                .ToArray();

            var deletedCount = await _database.KeyDeleteAsync(redisKeys);

            return deletedCount > 0;
        }

        /// <summary>
        /// Deletes the full resource bundle associated with the specified execution record.
        ///
        /// This overload is useful when the caller already has the record in memory
        /// and wants to avoid an additional store read.
        /// </summary>
        /// <param name="record">The execution record.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if at least one Redis key was deleted; otherwise false.</returns>
        public async Task<bool> DeleteExecutionBundleAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            var keys = new HashSet<string>(StringComparer.Ordinal)
            {
                GetExecutionRecordKey(record.ExecutionId),
                GetExecutionStateKey(record.ExecutionId)
            };

            if (!string.IsNullOrWhiteSpace(record.ContextKey))
            {
                keys.Add(GetRbacContextKey(record.ContextKey));
                keys.Add(GetRbacInFlightKey(record.ContextKey));
            }

            var redisKeys = keys
                .Select(x => (RedisKey)x)
                .ToArray();

            var deletedCount = await _database.KeyDeleteAsync(redisKeys);

            return deletedCount > 0;
        }

        /// <summary>
        /// Builds the Redis key used for AI execution records.
        /// </summary>
        private static string GetExecutionRecordKey(string executionId)
            => $"ai:execution:record:{executionId}";

        /// <summary>
        /// Builds the Redis key used for AI execution states.
        /// </summary>
        private static string GetExecutionStateKey(string executionId)
            => $"ai:execution:state:{executionId}";

        /// <summary>
        /// Builds the Redis key used for RBAC context payload.
        /// </summary>
        private static string GetRbacContextKey(string contextKey)
            => $"ac:ctx:{contextKey}";

        /// <summary>
        /// Builds the Redis key used for RBAC in-flight tracking.
        /// </summary>
        private static string GetRbacInFlightKey(string contextKey)
            => $"ac:inflight:{contextKey}";
    }
}