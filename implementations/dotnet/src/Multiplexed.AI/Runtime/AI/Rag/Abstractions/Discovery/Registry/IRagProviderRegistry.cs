using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery
{
    /// <summary>
    /// Defines a registry for RAG provider descriptors.
    ///
    /// PURPOSE:
    /// - Stores the set of discovered RAG providers.
    /// - Provides lookup capabilities by key.
    /// - Acts as a stable runtime index over provider metadata.
    ///
    /// DESIGN:
    /// - This registry is read-only after initialization.
    /// - It does not create provider instances.
    /// - It does not contain business logic or selection policies.
    ///
    /// USAGE:
    /// - Populated during application startup after discovery.
    /// - Used by orchestration layers to resolve providers by key.
    /// </summary>
    public interface IRagProviderRegistry
    {
        /// <summary>
        /// Gets all registered provider descriptors.
        /// </summary>
        IReadOnlyCollection<RagProviderDescriptor> GetAll();

        /// <summary>
        /// Gets a provider descriptor by its unique key.
        /// </summary>
        /// <param name="key">
        /// The provider key.
        /// </param>
        /// <returns>
        /// The matching provider descriptor.
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when no provider matches the specified key.
        /// </exception>
        RagProviderDescriptor GetByKey(string key);

        /// <summary>
        /// Attempts to get a provider descriptor by its key.
        /// </summary>
        /// <param name="key">
        /// The provider key.
        /// </param>
        /// <param name="descriptor">
        /// When this method returns, contains the descriptor if found.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if found; otherwise <see langword="false"/>.
        /// </returns>
        bool TryGetByKey(string key, out RagProviderDescriptor descriptor);
    }
}