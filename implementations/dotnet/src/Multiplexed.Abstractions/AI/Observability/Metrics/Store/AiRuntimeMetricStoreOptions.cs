namespace Multiplexed.Abstractions.AI.Observability.Metrics.Store
{
    /// <summary>
    /// Options controlling runtime metric persistence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These options allow the runtime host to choose whether metric records are
    /// disabled, kept in memory, or persisted to MongoDB.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeMetricStoreOptions
    {
        /// <summary>
        /// Gets or sets the metric store mode.
        /// </summary>
        public AiRuntimeMetricStoreMode Mode { get; set; } = AiRuntimeMetricStoreMode.Memory;

        /// <summary>
        /// Gets or sets the MongoDB connection string used when <see cref="Mode"/> is
        /// <see cref="AiRuntimeMetricStoreMode.Mongo"/>.
        /// </summary>
        public string MongoConnectionString { get; set; } = "mongodb://localhost:27017";

        /// <summary>
        /// Gets or sets the MongoDB database name used when <see cref="Mode"/> is
        /// <see cref="AiRuntimeMetricStoreMode.Mongo"/>.
        /// </summary>
        public string MongoDatabaseName { get; set; } = "multiplexed_ai_runtime";

        /// <summary>
        /// Gets or sets the MongoDB collection name used when <see cref="Mode"/> is
        /// <see cref="AiRuntimeMetricStoreMode.Mongo"/>.
        /// </summary>
        public string MongoCollectionName { get; set; } = "ai_runtime_metrics";
    }
}