namespace Multiplexed.Abstractions.AI.Rag.Enums
{
    /// <summary>
    /// Describes HOW data is retrieved.
    /// This is NOT the provider, but the retrieval strategy.
    /// </summary>
    public enum RagRetrievalKind
    {
        Unknown = 0,

        /// <summary>
        /// Semantic similarity (vector search).
        /// </summary>
        Vector = 1,

        /// <summary>
        /// Structured query (SQL, etc).
        /// </summary>
        Sql = 2,

        /// <summary>
        /// Runtime data (execution state, logs, etc).
        /// </summary>
        Runtime = 3,

        /// <summary>
        /// Multiple providers combined.
        /// </summary>
        MultiProvider = 4
    }
}