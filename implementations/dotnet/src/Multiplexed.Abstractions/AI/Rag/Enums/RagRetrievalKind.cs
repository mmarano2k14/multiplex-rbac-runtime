namespace Multiplexed.Abstractions.AI.Rag.Enums
{
    /// <summary>
    /// Describes HOW data is retrieved (functional intent).
    /// Not the provider, but the retrieval strategy at a logical level.
    /// </summary>
    public enum RagRetrievalKind
    {
        Unknown = 0,

        /// <summary>
        /// Retrieve by identifier (primary key lookup).
        /// </summary>
        ById = 1,

        /// <summary>
        /// Structured filtering (WHERE conditions).
        /// </summary>
        Filter = 2,

        /// <summary>
        /// Keyword or text-based search.
        /// </summary>
        Keyword = 3,

        /// <summary>
        /// Semantic similarity (vector search).
        /// </summary>
        Vector = 4,

        /// <summary>
        /// Combination of multiple strategies (SQL + Vector).
        /// </summary>
        Hybrid = 5
    }
}