using System.Reflection;

namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Discovers dynamically loadable RAG operations from assemblies.
    ///
    /// PURPOSE:
    /// - scan external/domain assemblies
    /// - locate classes marked with <see cref="RagOperationAttribute"/>
    /// - extract runtime metadata required by the registry
    ///
    /// IMPORTANT:
    /// - discovery happens at startup / registration time
    /// - runtime execution should rely on descriptors and registries, not repeated reflection
    /// </summary>
    public interface IRagOperationDiscoveryService
    {
        /// <summary>
        /// Discovers RAG operation descriptors from the provided assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// Assemblies to scan.
        /// </param>
        /// <returns>
        /// A deterministic collection of discovered operation descriptors.
        /// </returns>
        IReadOnlyCollection<RagOperationDescriptor> Discover(IEnumerable<Assembly> assemblies);
    }
}