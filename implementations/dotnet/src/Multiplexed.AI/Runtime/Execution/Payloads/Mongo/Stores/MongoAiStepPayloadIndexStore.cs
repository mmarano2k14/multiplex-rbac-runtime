using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Documents;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores
{
    /// <summary>
    /// MongoDB-backed archived step payload index store.
    ///
    /// PURPOSE:
    /// - Records which steps were evicted from hot execution state.
    /// - Allows the runtime to discover and reload archived step payloads.
    /// - Keeps the hot state clean without losing DAG history.
    ///
    /// DESIGN:
    /// - MongoDB is the durable source of truth for archived step indexes.
    /// - Payload content remains stored in the configured payload store.
    /// - Upserts are used so archiving the same step is idempotent.
    ///
    /// IMPORTANT:
    /// - This is not the payload store.
    /// - This is only the lookup/index layer for archived steps.
    /// </summary>
    public sealed class MongoAiStepPayloadIndexStore : IAiStepPayloadIndexStore
    {
        private readonly IMongoCollection<MongoAiStepPayloadIndexDocument> _collection;

        /// <summary>
        /// Initializes a new instance of the <see cref="MongoAiStepPayloadIndexStore"/> class.
        /// </summary>
        public MongoAiStepPayloadIndexStore(
            IOptions<AiPayloadStoreOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var mongo = options.Value.Mongo;

            if (mongo is null || !mongo.Enabled)
            {
                throw new InvalidOperationException(
                    "Mongo step payload index store requires Mongo payload store configuration.");
            }

            if (string.IsNullOrWhiteSpace(mongo.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Mongo step payload index store connection string is required.");
            }

            if (string.IsNullOrWhiteSpace(mongo.DatabaseName))
            {
                throw new InvalidOperationException(
                    "Mongo step payload index store database name is required.");
            }

            var collectionName = $"{mongo.CollectionName}_step_index";

            var client = new MongoClient(mongo.ConnectionString);
            var database = client.GetDatabase(mongo.DatabaseName);
            _collection = database.GetCollection<MongoAiStepPayloadIndexDocument>(collectionName);
        }

        /// <inheritdoc />
        public async Task MarkArchivedAsync(
            AiArchivedStepPayloadIndex entry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.ExecutionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.StepName);
            ArgumentNullException.ThrowIfNull(entry.Payload);

            var id = MongoAiStepPayloadIndexDocument.BuildId(
                entry.ExecutionId,
                entry.StepName);

            var document = new MongoAiStepPayloadIndexDocument
            {
                Id = id,
                ExecutionId = entry.ExecutionId,
                StepName = entry.StepName,
                Status = entry.Status,
                Payload = entry.Payload,
                ArchivedAtUtc = entry.ArchivedAtUtc == default
                    ? DateTime.UtcNow
                    : entry.ArchivedAtUtc,
                Reason = string.IsNullOrWhiteSpace(entry.Reason)
                    ? "retention"
                    : entry.Reason
            };

            await _collection.ReplaceOneAsync(
                    x => x.Id == id,
                    document,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AiArchivedStepPayloadIndex?> GetAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            var id = MongoAiStepPayloadIndexDocument.BuildId(
                executionId,
                stepName);

            var document = await _collection
                .Find(x => x.Id == id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return document is null ? null : ToModel(document);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            var documents = await _collection
                .Find(x => x.ExecutionId == executionId)
                .SortBy(x => x.ArchivedAtUtc)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return documents
                .Select(ToModel)
                .ToList();
        }

        /// <inheritdoc />
        public async Task DeleteAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            var id = MongoAiStepPayloadIndexDocument.BuildId(
                executionId,
                stepName);

            await _collection.DeleteOneAsync(
                    x => x.Id == id,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
            string executionId,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(stepNames);

            var names = stepNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (names.Length == 0)
            {
                return new Dictionary<string, AiArchivedStepPayloadIndex>(StringComparer.Ordinal);
            }

            var documents = await _collection
                .Find(x => x.ExecutionId == executionId && names.Contains(x.StepName))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return documents
                .Select(ToModel)
                .ToDictionary(x => x.StepName, StringComparer.Ordinal);
        }

        private static AiArchivedStepPayloadIndex ToModel(
            MongoAiStepPayloadIndexDocument document)
        {
            return new AiArchivedStepPayloadIndex
            {
                ExecutionId = document.ExecutionId,
                StepName = document.StepName,
                Status = document.Status,
                Payload = document.Payload,
                ArchivedAtUtc = document.ArchivedAtUtc,
                Reason = document.Reason
            };
        }
    }
}