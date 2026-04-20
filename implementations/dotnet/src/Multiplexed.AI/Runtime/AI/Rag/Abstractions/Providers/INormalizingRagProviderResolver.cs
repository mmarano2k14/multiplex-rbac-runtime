using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers
{
    /// <summary>
    /// Resolves a normalizing RAG provider by its configured key.
    ///
    /// PURPOSE:
    /// - Decouples step definitions from direct provider construction.
    /// - Allows providers to be discovered and resolved dynamically.
    ///
    /// DESIGN:
    /// - Resolution is key-based and deterministic.
    /// - Implementations typically rely on registries and dependency injection.
    /// </summary>
    public interface INormalizingRagProviderResolver
    {
        /// <summary>
        /// Resolves a provider instance by its unique key.
        /// </summary>
        /// <param name="providerKey">
        /// The configured provider key.
        /// </param>
        /// <returns>
        /// The resolved <see cref="INormalizingRagProvider"/>.
        /// </returns>
        INormalizingRagProvider Resolve(string providerKey);
    }
}