namespace Multiplexed.Abstractions.AI.Tracing.Store
{
    /// <summary>
    /// Options controlling runtime trace persistence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These options allow the runtime host to choose whether trace records are
    /// disabled, kept in memory, persisted to MongoDB, or both.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeTraceStoreOptions
    {
        /// <summary>
        /// Gets or sets the trace store mode.
        /// </summary>
        public AiRuntimeTraceStoreMode Mode { get; set; } = AiRuntimeTraceStoreMode.Memory;

        /// <summary>
        /// Gets or sets the MongoDB connection string used when <see cref="Mode"/> is
        /// <see cref="AiRuntimeTraceStoreMode.Mongo"/> or
        /// <see cref="AiRuntimeTraceStoreMode.MemoryAndMongo"/>.
        /// </summary>
        public string MongoConnectionString { get; set; } = "mongodb://localhost:27017";

        /// <summary>
        /// Gets or sets the MongoDB database name used when <see cref="Mode"/> is
        /// <see cref="AiRuntimeTraceStoreMode.Mongo"/> or
        /// <see cref="AiRuntimeTraceStoreMode.MemoryAndMongo"/>.
        /// </summary>
        public string MongoDatabaseName { get; set; } = "multiplexed_ai_runtime";

        /// <summary>
        /// Gets or sets the MongoDB collection name used when <see cref="Mode"/> is
        /// <see cref="AiRuntimeTraceStoreMode.Mongo"/> or
        /// <see cref="AiRuntimeTraceStoreMode.MemoryAndMongo"/>.
        /// </summary>
        public string MongoCollectionName { get; set; } = "ai_runtime_traces";
    }
}