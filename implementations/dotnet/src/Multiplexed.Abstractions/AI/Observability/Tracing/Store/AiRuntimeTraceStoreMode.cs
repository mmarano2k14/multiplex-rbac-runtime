namespace Multiplexed.Abstractions.AI.Observability.Tracing.Store
{
    /// <summary>
    /// Defines how runtime trace records are stored.
    /// </summary>
    public enum AiRuntimeTraceStoreMode
    {
        /// <summary>
        /// Trace persistence is disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Trace records are stored in memory.
        /// </summary>
        Memory = 1,

        /// <summary>
        /// Trace records are persisted to MongoDB.
        /// </summary>
        Mongo = 2,

        /// <summary>
        /// Trace records are stored in memory and persisted to MongoDB.
        /// </summary>
        MemoryAndMongo = 3
    }
}