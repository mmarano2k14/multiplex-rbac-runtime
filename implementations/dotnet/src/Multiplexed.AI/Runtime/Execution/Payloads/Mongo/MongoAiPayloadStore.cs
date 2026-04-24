using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Multiplexed.Abstractions.AI.Execution.Payloads;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Mongo
{
    /// <summary>
    /// MongoDB-backed payload store.
    ///
    /// PURPOSE:
    /// - Persists externalized execution payloads durably.
    /// - Keeps large payload content outside execution state and snapshots.
    /// - Enables replay and recovery by resolving payload references after restart.
    ///
    /// DESIGN:
    /// - MongoDB is the source of truth.
    /// - Payload ids are stable GUID-like strings.
    /// - Payload content is stored as serialized text, usually JSON.
    ///
    /// IMPORTANT:
    /// - This store is replay-safe.
    /// - Payload documents must not expire before their related snapshots.
    /// - Missing payloads are treated as invalid replay/recovery state by the resolver.
    /// </summary>
    public sealed class MongoAiPayloadStore : IAiPayloadStore
    {
        private readonly IMongoCollection<MongoAiPayloadDocument> _collection;

        /// <summary>
        /// Initializes a new instance of the <see cref="MongoAiPayloadStore"/> class.
        /// </summary>
        public MongoAiPayloadStore(
            IOptions<AiPayloadStoreOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var mongo = options.Value.Mongo;

            if (mongo is null || !mongo.Enabled)
            {
                throw new InvalidOperationException(
                    "Mongo payload store is not enabled.");
            }

            if (string.IsNullOrWhiteSpace(mongo.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Mongo payload store connection string is required.");
            }

            if (string.IsNullOrWhiteSpace(mongo.DatabaseName))
            {
                throw new InvalidOperationException(
                    "Mongo payload store database name is required.");
            }

            if (string.IsNullOrWhiteSpace(mongo.CollectionName))
            {
                throw new InvalidOperationException(
                    "Mongo payload store collection name is required.");
            }

            var client = new MongoClient(mongo.ConnectionString);
            var database = client.GetDatabase(mongo.DatabaseName);
            _collection = database.GetCollection<MongoAiPayloadDocument>(mongo.CollectionName);
        }

        /// <summary>
        /// Saves serialized payload content to MongoDB and returns the payload id.
        /// </summary>
        public async Task<string> SaveAsync(
            string content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            var now = DateTime.UtcNow;
            var id = Guid.NewGuid().ToString("N");

            var document = new MongoAiPayloadDocument
            {
                Id = id,
                Content = content,
                SizeBytes = content.Length,
                ContentType = "application/json",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await _collection.InsertOneAsync(
                document,
                cancellationToken: cancellationToken);

            return id;
        }

        /// <summary>
        /// Loads serialized payload content from MongoDB.
        ///
        /// Returns null when no payload exists for the specified id.
        /// </summary>
        public async Task<string?> LoadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var document = await _collection
                .Find(x => x.Id == key)
                .FirstOrDefaultAsync(cancellationToken);

            return document?.Content;
        }

        /// <summary>
        /// Deletes payload content from MongoDB.
        /// </summary>
        public async Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            await _collection.DeleteOneAsync(
                x => x.Id == key,
                cancellationToken);
        }
    }
}