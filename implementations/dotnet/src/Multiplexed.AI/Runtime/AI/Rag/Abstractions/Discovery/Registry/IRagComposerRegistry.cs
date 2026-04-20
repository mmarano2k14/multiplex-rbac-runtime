using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry
{
    /// <summary>
    /// Defines a registry for RAG composer descriptors.
    ///
    /// PURPOSE:
    /// - Stores discovered composer implementations.
    /// - Enables lookup of composition strategies by key.
    ///
    /// DESIGN:
    /// - Read-only after initialization.
    /// - Metadata-only (no instantiation).
    /// - Keeps composition discovery decoupled from execution.
    ///
    /// USAGE:
    /// - Used by composition orchestration layers to resolve
    ///   composers dynamically.
    /// </summary>
    public interface IRagComposerRegistry
    {
        /// <summary>
        /// Gets all registered composer descriptors.
        /// </summary>
        IReadOnlyCollection<RagComposerDescriptor> GetAll();

        /// <summary>
        /// Gets a composer descriptor by its unique key.
        /// </summary>
        /// <param name="key">
        /// The composer key.
        /// </param>
        /// <returns>
        /// The matching composer descriptor.
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when no composer matches the specified key.
        /// </exception>
        RagComposerDescriptor GetByKey(string key);

        /// <summary>
        /// Attempts to get a composer descriptor by its key.
        /// </summary>
        /// <param name="key">
        /// The composer key.
        /// </param>
        /// <param name="descriptor">
        /// When this method returns, contains the descriptor if found.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if found; otherwise <see langword="false"/>.
        /// </returns>
        bool TryGetByKey(string key, out RagComposerDescriptor descriptor);
    }
}