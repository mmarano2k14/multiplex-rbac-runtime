using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery
{
    /// <summary>
    /// Defines a registry for RAG retrieval descriptors.
    ///
    /// PURPOSE:
    /// - Stores discovered retrieval implementations.
    /// - Provides lookup capabilities for retrieval orchestration.
    ///
    /// DESIGN:
    /// - Read-only after initialization.
    /// - Contains metadata only, not instances.
    /// - Keeps retrieval discovery separate from execution logic.
    ///
    /// USAGE:
    /// - Used by orchestration layers or factories to resolve retrieval
    ///   implementations by key.
    /// </summary>
    public interface IRagRetrievalRegistry
    {
        /// <summary>
        /// Gets all registered retrieval descriptors.
        /// </summary>
        IReadOnlyCollection<RagRetrievalDescriptor> GetAll();

        /// <summary>
        /// Gets a retrieval descriptor by its unique key.
        /// </summary>
        /// <param name="key">
        /// The retrieval key.
        /// </param>
        /// <returns>
        /// The matching retrieval descriptor.
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when no retrieval matches the specified key.
        /// </exception>
        RagRetrievalDescriptor GetByKey(string key);

        /// <summary>
        /// Attempts to get a retrieval descriptor by its key.
        /// </summary>
        /// <param name="key">
        /// The retrieval key.
        /// </param>
        /// <param name="descriptor">
        /// When this method returns, contains the descriptor if found.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if found; otherwise <see langword="false"/>.
        /// </returns>
        bool TryGetByKey(string key, out RagRetrievalDescriptor descriptor);
    }
}