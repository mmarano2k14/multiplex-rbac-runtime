using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Snapshot.Mongo
{
    /// <summary>
    /// Provides MongoDB index initialization for AI execution snapshot persistence.
    ///
    /// DESIGN:
    /// - Index creation is idempotent
    /// - This helper is intended to be called once during application startup
    /// - The snapshot collection is optimized for:
    ///   - direct lookup by execution id
    ///   - filtering by pipeline name
    ///   - filtering by execution status
    ///   - recent activity inspection
    /// </summary>
    public static class MongoAiExecutionSnapshotIndexes
    {
        /// <summary>
        /// Ensures the required MongoDB indexes exist for the execution snapshot collection.
        /// </summary>
        /// <typeparam name="TContextSnapshot">
        /// The serializable external context snapshot type associated with the execution.
        /// </typeparam>
        /// <param name="collection">The Mongo snapshot collection.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public static async Task EnsureCreatedAsync<TContextSnapshot>(
            IMongoCollection<AiExecutionSnapshotDocument<TContextSnapshot>> collection,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(collection);

            var executionIdIndex = new CreateIndexModel<AiExecutionSnapshotDocument<TContextSnapshot>>(
                Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                    .IndexKeys
                    .Ascending(x => x.ExecutionId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_ai_execution_snapshot_execution_id"
                });

            var pipelineNameIndex = new CreateIndexModel<AiExecutionSnapshotDocument<TContextSnapshot>>(
                Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                    .IndexKeys
                    .Ascending(x => x.PipelineName),
                new CreateIndexOptions
                {
                    Name = "ix_ai_execution_snapshot_pipeline_name"
                });

            var statusIndex = new CreateIndexModel<AiExecutionSnapshotDocument<TContextSnapshot>>(
                Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                    .IndexKeys
                    .Ascending(x => x.Status),
                new CreateIndexOptions
                {
                    Name = "ix_ai_execution_snapshot_status"
                });

            var updatedAtUtcIndex = new CreateIndexModel<AiExecutionSnapshotDocument<TContextSnapshot>>(
                Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                    .IndexKeys
                    .Descending(x => x.UpdatedAtUtc),
                new CreateIndexOptions
                {
                    Name = "ix_ai_execution_snapshot_updated_at_utc_desc"
                });

            await collection.Indexes.CreateManyAsync(
                new[]
                {
                    executionIdIndex,
                    pipelineNameIndex,
                    statusIndex,
                    updatedAtUtcIndex
                },
                cancellationToken);
        }
    }
}