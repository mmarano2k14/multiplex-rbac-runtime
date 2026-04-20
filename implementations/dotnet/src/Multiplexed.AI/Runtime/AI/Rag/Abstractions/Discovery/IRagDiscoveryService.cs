using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using System.Collections.Generic;
using System.Reflection;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery
{
    /// <summary>
    /// Defines the contract for discovering RAG components from assemblies.
    ///
    /// PURPOSE:
    /// - Scans assemblies for classes decorated with RAG discovery attributes.
    /// - Produces normalized descriptor models for providers, retrievals,
    ///   and composers.
    /// - Centralizes reflection-based discovery behind a single abstraction.
    ///
    /// DESIGN:
    /// - This service performs discovery only.
    /// - It does not instantiate implementations.
    /// - It does not register services in dependency injection.
    /// - It does not apply runtime selection policies.
    ///
    /// USAGE:
    /// - Called during startup or registration flows to discover available
    ///   RAG implementations.
    /// - The resulting descriptors can then be stored in registries or used
    ///   to drive DI registration.
    /// </summary>
    public interface IRagDiscoveryService
    {
        /// <summary>
        /// Discovers provider implementations from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan.
        /// </param>
        /// <returns>
        /// A read-only list of discovered provider descriptors.
        /// </returns>
        IReadOnlyList<RagProviderDescriptor> DiscoverProviders(params Assembly[] assemblies);

        /// <summary>
        /// Discovers retrieval implementations from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan.
        /// </param>
        /// <returns>
        /// A read-only list of discovered retrieval descriptors.
        /// </returns>
        IReadOnlyList<RagRetrievalDescriptor> DiscoverRetrievals(params Assembly[] assemblies);

        /// <summary>
        /// Discovers composer implementations from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan.
        /// </param>
        /// <returns>
        /// A read-only list of discovered composer descriptors.
        /// </returns>
        IReadOnlyList<RagComposerDescriptor> DiscoverComposers(params Assembly[] assemblies);
    }
}