namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Resolves a registered RAG operation by key.
    /// </summary>
    public interface IRagOperationResolver
    {
        /// <summary>
        /// Resolves a RAG operation by its unique key.
        /// </summary>
        /// <param name="key">
        /// Operation key.
        /// </param>
        /// <returns>
        /// Resolved operation instance.
        /// </returns>
        IRagOperation Resolve(string key);
    }
}