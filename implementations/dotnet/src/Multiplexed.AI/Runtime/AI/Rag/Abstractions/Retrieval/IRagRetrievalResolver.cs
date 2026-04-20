using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval
{
    /// <summary>
    /// Resolves a RAG retrieval strategy by its configured key.
    ///
    /// PURPOSE:
    /// - Decouples pipeline steps from concrete retrieval implementations.
    /// - Enables dynamic selection of retrieval strategies via configuration.
    ///
    /// DESIGN:
    /// - Resolution is key-based and deterministic.
    /// - Backed by descriptor registry + dependency injection.
    /// </summary>
    public interface IRagRetrievalResolver
    {
        /// <summary>
        /// Resolves a retrieval strategy by key.
        /// </summary>
        /// <param name="retrievalKey">
        /// The configured retrieval key.
        /// </param>
        /// <returns>
        /// The resolved <see cref="IRagRetrieval"/> instance.
        /// </returns>
        IRagRetrieval Resolve(string retrievalKey);
    }
}