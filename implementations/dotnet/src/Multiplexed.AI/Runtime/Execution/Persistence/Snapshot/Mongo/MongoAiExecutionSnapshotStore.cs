using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot;
using Multiplexed.AI.Configuration;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Snapshot.Mongo
{
    /// <summary>
    /// MongoDB implementation of <see cref="IAiExecutionSnapshotStore{TContextSnapshot}"/>.
    ///
    /// PURPOSE:
    /// - Persist durable execution snapshots for inspection, audit, replay support,
    ///   and post-mortem debugging
    /// - Keep persistence concerns isolated from the execution engine
    ///
    /// DESIGN:
    /// - Uses one document per execution
    /// - ExecutionId is the durable identity and must be unique
    /// - The runtime coordination store remains the source of truth for distributed execution
    /// - MongoDB here acts as a durable snapshot store, not as the live coordination layer
    ///
    /// PERSISTENCE MODEL:
    /// - Uses upsert semantics
    /// - Immutable fields are written with SetOnInsert
    /// - Mutable fields are updated with Set
    ///
    /// This keeps persistence idempotent and avoids replacing the full document
    /// unnecessarily.
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The serializable external context snapshot type associated with the execution.
    /// </typeparam>
    public sealed class MongoAiExecutionSnapshotStore<TContextSnapshot> : IAiExecutionSnapshotStore<TContextSnapshot>
    {
        private readonly IMongoCollection<AiExecutionSnapshotDocument<TContextSnapshot>> _collection;
        private readonly ILogger<MongoAiExecutionSnapshotStore<TContextSnapshot>> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MongoAiExecutionSnapshotStore{TContextSnapshot}"/> class.
        /// </summary>
        /// <param name="database">
        /// The Mongo database instance.
        /// The connection string and database selection are expected to be configured
        /// at the dependency injection level.
        /// </param>
        /// <param name="options">The Mongo snapshot options.</param>
        /// <param name="logger">The logger.</param>
        public MongoAiExecutionSnapshotStore(
            IMongoDatabase database,
            AiExecutionSnapshotMongoOptions options,
            ILogger<MongoAiExecutionSnapshotStore<TContextSnapshot>> logger)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(logger);

            if (string.IsNullOrWhiteSpace(options.CollectionName))
            {
                throw new InvalidOperationException(
                    "AI execution snapshot Mongo collection name cannot be null or empty.");
            }

            _collection = database.GetCollection<AiExecutionSnapshotDocument<TContextSnapshot>>(
                options.CollectionName);

            _logger = logger;
        }

        /// <inheritdoc />
        public async Task UpsertAsync(
            AiExecutionSnapshotDocument<TContextSnapshot> snapshot,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ExecutionId);

            var utcNow = DateTime.UtcNow;
            snapshot.UpdatedAtUtc = utcNow;

            var filter = Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                .Filter
                .Eq(x => x.ExecutionId, snapshot.ExecutionId);

            var update = Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                .Update
                .SetOnInsert(x => x.ExecutionId, snapshot.ExecutionId)
                .SetOnInsert(x => x.CreatedAtUtc, snapshot.CreatedAtUtc)
                .Set(x => x.PipelineName, snapshot.PipelineName)
                .Set(x => x.Status, snapshot.Status)
                .Set(x => x.ContextKey, snapshot.ContextKey)
                .Set(x => x.ContextSnapshot, snapshot.ContextSnapshot)
                .Set(x => x.UpdatedAtUtc, snapshot.UpdatedAtUtc)
                .Set(x => x.CompletedAtUtc, snapshot.CompletedAtUtc)
                .Set(x => x.Record, snapshot.Record)
                .Set(x => x.State, snapshot.State)
                .Set(x => x.Steps, snapshot.Steps)
                .Set(x => x.Events, snapshot.Events);

            try
            {
                await _collection.UpdateOneAsync(
                    filter,
                    update,
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken);

                _logger.LogDebug(
                    "AI execution snapshot upserted for execution {ExecutionId}.",
                    snapshot.ExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to upsert AI execution snapshot for execution {ExecutionId}.",
                    snapshot.ExecutionId);

                _logger.LogError(
                    ex,
                    "Failed to upsert AI execution snapshot for execution {ExecutionId}. Error={Error}",
                    snapshot.ExecutionId,
                    ex.ToString());

                Console.WriteLine(ex.ToString());

                throw;
            }
        }

        /// <inheritdoc />
        public async Task<AiExecutionSnapshotDocument<TContextSnapshot>?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            var filter = Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                .Filter
                .Eq(x => x.ExecutionId, executionId);

            try
            {
                return await _collection
                    .Find(filter)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load AI execution snapshot for execution {ExecutionId}.",
                    executionId);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            var filter = Builders<AiExecutionSnapshotDocument<TContextSnapshot>>
                .Filter
                .Eq(x => x.ExecutionId, executionId);

            try
            {
                await _collection.DeleteOneAsync(filter, cancellationToken);

                _logger.LogDebug(
                    "AI execution snapshot deleted for execution {ExecutionId}.",
                    executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete AI execution snapshot for execution {ExecutionId}.",
                    executionId);

                throw;
            }
        }
    }
}