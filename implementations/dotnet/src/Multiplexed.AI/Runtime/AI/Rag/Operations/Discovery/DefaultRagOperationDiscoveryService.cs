using System.Reflection;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;

namespace Multiplexed.AI.Runtime.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Default reflection-based RAG operation discovery service.
    ///
    /// FEATURES:
    /// - scans one or more assemblies
    /// - supports partial type loading
    /// - validates concrete operation implementations
    /// - extracts execution context type from IRagOperation&lt;TExecutionContext&gt;
    /// - returns deterministic results
    /// </summary>
    public sealed class DefaultRagOperationDiscoveryService : IRagOperationDiscoveryService
    {
        /// <inheritdoc />
        public IReadOnlyCollection<RagOperationDescriptor> Discover(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var assemblyList = assemblies
                .Where(x => x != null)
                .Distinct()
                .OrderBy(x => x.FullName, StringComparer.Ordinal)
                .ToArray();

            var descriptors = new List<RagOperationDescriptor>();

            foreach (var assembly in assemblyList)
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type == null)
                    {
                        continue;
                    }

                    if (!type.IsClass || type.IsAbstract)
                    {
                        continue;
                    }

                    var attribute = type.GetCustomAttribute<RagOperationAttribute>(inherit: false);
                    if (attribute == null)
                    {
                        continue;
                    }

                    RagOperationDescriptorExtensions.ValidateOperationImplementationType(type);

                    var executionContextType =
                        RagOperationDescriptorExtensions.ResolveExecutionContextType(type);

                    descriptors.Add(new RagOperationDescriptor
                    {
                        Key = attribute.Key,
                        ImplementationType = type,
                        ExecutionContextType = executionContextType
                    });
                }
            }

            return descriptors
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ThenBy(x => x.ImplementationType.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Safely gets loadable types from an assembly, supporting partial load failures.
        /// </summary>
        /// <param name="assembly">
        /// Assembly to inspect.
        /// </param>
        /// <returns>
        /// Loadable types.
        /// </returns>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(x => x != null)!;
            }
        }
    }
}