using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition
{
    /// <summary>
    /// Resolves a RAG composer by its configured key.
    ///
    /// PURPOSE:
    /// - Decouples pipeline steps from concrete composer implementations.
    /// - Enables dynamic composition strategies via configuration.
    ///
    /// DESIGN:
    /// - Resolution is key-based and deterministic.
    /// - Backed by descriptor registry + dependency injection.
    /// </summary>
    public interface IRagComposerResolver
    {
        /// <summary>
        /// Resolves a composer by key.
        /// </summary>
        /// <param name="composerKey">
        /// The configured composer key.
        /// </param>
        /// <returns>
        /// The resolved composer instance.
        /// </returns>
        IRagComposer<RagStructuredContext> Resolve(string composerKey);
    }
}