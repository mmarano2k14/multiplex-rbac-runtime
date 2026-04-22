namespace Multiplexed.Abstractions.AI.Rag.Enums
{
    /// <summary>
    /// Describes the FUNCTIONAL role of a provider.
    /// Not the technology, but what it does.
    ///
    /// PURPOSE:
    /// - Enables orchestration decisions (merge, rank, hybrid retrieval)
    /// - Allows distinguishing between structured, semantic, and dynamic data sources
    /// - Supports multi-provider RAG pipelines
    /// </summary>
    public enum RagProviderKind
    {
        Unknown = 0,

        /// <summary>
        /// Structured data retrieval (SQL, relational, strongly typed records)
        /// </summary>
        Structured = 1,

        /// <summary>
        /// Semantic/vector retrieval (embeddings, similarity search)
        /// </summary>
        Vector = 2,

        /// <summary>
        /// Unstructured text retrieval (documents, files, blobs)
        /// </summary>
        Unstructured = 3,

        /// <summary>
        /// Runtime/state-based retrieval (execution state, in-memory, pipeline data)
        /// </summary>
        Runtime = 4,

        /// <summary>
        /// External API retrieval (remote services, HTTP, SaaS)
        /// </summary>
        External = 5
    }
}