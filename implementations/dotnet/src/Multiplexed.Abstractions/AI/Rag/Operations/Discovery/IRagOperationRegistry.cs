namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Registry abstraction for discovered RAG operations.
    /// </summary>
    public interface IRagOperationRegistry
    {
        /// <summary>
        /// Gets a registered operation descriptor by key.
        /// </summary>
        /// <param name="key">
        /// Operation key.
        /// </param>
        /// <returns>
        /// Matching descriptor.
        /// </returns>
        RagOperationDescriptor Get(string key);

        /// <summary>
        /// Attempts to resolve a registered operation descriptor by key.
        /// </summary>
        /// <param name="key">
        /// Operation key.
        /// </param>
        /// <param name="descriptor">
        /// Resolved descriptor if found.
        /// </param>
        /// <returns>
        /// True if found; otherwise false.
        /// </returns>
        bool TryGet(string key, out RagOperationDescriptor descriptor);

        /// <summary>
        /// Gets all registered RAG operation descriptors.
        /// </summary>
        IReadOnlyCollection<RagOperationDescriptor> GetAll();
    }
}