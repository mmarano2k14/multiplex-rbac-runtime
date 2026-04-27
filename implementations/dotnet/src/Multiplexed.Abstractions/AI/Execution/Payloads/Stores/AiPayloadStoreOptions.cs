using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;

namespace Multiplexed.Abstractions.AI.Execution.Payloads.Stores
{
    /// <summary>
    /// Configures execution payload storage.
    ///
    /// PURPOSE:
    /// - Controls where large execution payloads are stored.
    /// - Keeps payload storage configurable without changing execution logic.
    /// - Allows the runtime to use MongoDB as durable storage and Redis as an optional cache.
    ///
    /// IMPORTANT:
    /// - Payload storage is part of replay/recovery safety when payloads are externalized.
    /// - In-memory storage is suitable only for tests and development.
    /// </summary>
    public sealed class AiPayloadStoreOptions
    {
        /// <summary>
        /// Gets or sets whether external payload storage is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the storage provider.
        ///
        /// Supported values:
        /// - inmemory
        /// - mongo
        /// - mongo-redis
        ///
        /// RECOMMENDED:
        /// - Use mongo for durable replay-safe payloads.
        /// - Use mongo-redis when Redis should act as a bounded cache in front of MongoDB.
        /// </summary>
        public string Provider { get; set; } = "inmemory";

        /// <summary>
        /// Gets or sets whether payloads must be replay-safe.
        ///
        /// When true, non-durable providers should not be used outside tests.
        /// </summary>
        public bool RequireReplaySafePayloads { get; set; } = true;

        /// <summary>
        /// Gets or sets MongoDB payload store options.
        /// </summary>
        public MongoAiPayloadStoreOptions Mongo { get; set; } = new();

        /// <summary>
        /// Gets or sets Redis payload cache options.
        /// </summary>
        public RedisAiPayloadCacheOptions RedisCache { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum serialized payload size allowed inline before
        /// externalizing to the configured payload store.
        ///
        /// PURPOSE:
        /// - Allows tests and runtime profiles to control when compaction starts.
        /// - Keeps production defaults safe while allowing integration tests to force externalization.
        /// </summary>
        public int MaxInlineSizeBytes { get; set; } = 2048;

        // <summary>
        /// Gets or sets Redis step index payload cache options.
        /// </summary>
        public RedisAiStepPayloadIndexCacheOptions StepIndexCache { get; set; } = new();
    }
}