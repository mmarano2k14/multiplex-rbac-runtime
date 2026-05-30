namespace Multiplexed.Abstractions.AI.Observability.Metrics.Store
{
    /// <summary>
    /// Defines how runtime metric records are stored.
    /// </summary>
    public enum AiRuntimeMetricStoreMode
    {
        /// <summary>
        /// Metric persistence is disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Metric records are stored in memory.
        /// </summary>
        Memory = 1,

        /// <summary>
        /// Metric records are persisted to MongoDB.
        /// </summary>
        Mongo = 2,

        /// <summary>
        /// Metric records are stored in memory and persisted to MongoDB.
        /// </summary>
        MemoryAndMongo = 3
    }
}