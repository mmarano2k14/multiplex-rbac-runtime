using System.Reflection;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Attributes;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;

namespace Multiplexed.AI.Runtime.AI.Rag.Discovery
{
    /// <summary>
    /// Default reflection-based implementation of <see cref="IRagDiscoveryService"/>.
    ///
    /// PURPOSE:
    /// - Scans assemblies for RAG components decorated with discovery attributes.
    /// - Converts reflected metadata into normalized descriptor models.
    /// - Provides a single reusable implementation for startup and registration flows.
    ///
    /// DESIGN:
    /// - Discovery is attribute-driven.
    /// - Only concrete, non-abstract, non-interface types are considered.
    /// - This service performs metadata discovery only and does not instantiate
    ///   any implementation types.
    ///
    /// USAGE:
    /// - Typically used during application startup.
    /// - The discovered descriptors can then be registered into registries
    ///   or used by dependency injection extensions.
    /// </summary>
    public sealed class DefaultRagDiscoveryService : IRagDiscoveryService
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
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="assemblies"/> is <see langword="null"/>.
        /// </exception>
        public IReadOnlyList<RagProviderDescriptor> DiscoverProviders(params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            return GetCandidateTypes(assemblies)
                .Select(type => new
                {
                    Type = type,
                    Attribute = type.GetCustomAttribute<RagProviderAttribute>(inherit: false)
                })
                .Where(x => x.Attribute is not null)
                .Select(x => new RagProviderDescriptor
                {
                    Key = x.Attribute!.Key,
                    ImplementationType = x.Type,
                    ProviderKind = x.Attribute.ProviderKind,
                    SourceType = x.Attribute.SourceType,
                    DisplayName = x.Attribute.DisplayName,
                    IsDefault = x.Attribute.IsDefault,
                    Status = x.Attribute.Status
                })
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Discovers retrieval implementations from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan.
        /// </param>
        /// <returns>
        /// A read-only list of discovered retrieval descriptors.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="assemblies"/> is <see langword="null"/>.
        /// </exception>
        public IReadOnlyList<RagRetrievalDescriptor> DiscoverRetrievals(params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            return GetCandidateTypes(assemblies)
                .Select(type => new
                {
                    Type = type,
                    Attribute = type.GetCustomAttribute<RagRetrievalAttribute>(inherit: false)
                })
                .Where(x => x.Attribute is not null)
                .Select(x => new RagRetrievalDescriptor
                {
                    Key = x.Attribute!.Key,
                    ImplementationType = x.Type,
                    Kind = x.Attribute.Kind,
                    DisplayName = x.Attribute.DisplayName,
                    IsDefault = x.Attribute.IsDefault
                })
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Discovers composer implementations from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan.
        /// </param>
        /// <returns>
        /// A read-only list of discovered composer descriptors.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="assemblies"/> is <see langword="null"/>.
        /// </exception>
        public IReadOnlyList<RagComposerDescriptor> DiscoverComposers(params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            return GetCandidateTypes(assemblies)
                .Select(type => new
                {
                    Type = type,
                    Attribute = type.GetCustomAttribute<RagComposerAttribute>(inherit: false)
                })
                .Where(x => x.Attribute is not null)
                .Select(x => new RagComposerDescriptor
                {
                    Key = x.Attribute!.Key,
                    ImplementationType = x.Type,
                    Kind = x.Attribute.Kind,
                    DisplayName = x.Attribute.DisplayName,
                    IsDefault = x.Attribute.IsDefault
                })
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Extracts the set of concrete candidate types from the specified assemblies.
        ///
        /// RULES:
        /// - Skip null assemblies.
        /// - Skip abstract classes.
        /// - Skip interfaces.
        /// - Keep only concrete types that can represent real implementations.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to inspect.
        /// </param>
        /// <returns>
        /// A sequence of concrete candidate types.
        /// </returns>
        private static IEnumerable<Type> GetCandidateTypes(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies.Where(a => a is not null).Distinct())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some assemblies may partially fail to load types because of
                    // optional dependencies. In that case, keep the successfully
                    // loaded types and continue discovery.
                    types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
                }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    yield return type;
                }
            }
        }
    }
}