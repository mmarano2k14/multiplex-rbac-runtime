using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Operations.Discovery;
using Multiplexed.AI.Runtime.AI.Rag.Steps;

namespace Multiplexed.AI.Runtime.AI.Rag.DI
{
    /// <summary>
    /// Dependency injection extensions for RAG operation discovery and registration.
    /// </summary>
    public static class RagOperationServiceCollectionExtensions
    {
        /// <summary>
        /// Discovers and registers RAG operations from the provided assemblies.
        ///
        /// REGISTERS:
        /// - <see cref="IRagOperationDiscoveryService"/>
        /// - <see cref="IRagOperationRegistry"/>
        /// - <see cref="IRagOperationResolver"/>
        /// - <see cref="IRagRetrievalStepDispatcher"/>
        /// - discovered operation implementation types
        /// </summary>
        /// <param name="services">
        /// Service collection.
        /// </param>
        /// <param name="assemblies">
        /// Assemblies to scan.
        /// </param>
        /// <returns>
        /// The same service collection for chaining.
        /// </returns>
        public static IServiceCollection AddRagOperationsFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            return services.AddRagOperationsFromAssemblies((IEnumerable<Assembly>)assemblies);
        }

        /// <summary>
        /// Discovers and registers RAG operations from the provided assemblies.
        /// </summary>
        /// <param name="services">
        /// Service collection.
        /// </param>
        /// <param name="assemblies">
        /// Assemblies to scan.
        /// </param>
        /// <returns>
        /// The same service collection for chaining.
        /// </returns>
        public static IServiceCollection AddRagOperationsFromAssemblies(
            this IServiceCollection services,
            IEnumerable<Assembly> assemblies)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var assemblyList = assemblies
                .Where(x => x != null)
                .Distinct()
                .OrderBy(x => x.FullName, StringComparer.Ordinal)
                .ToArray();

            // ---------------------------------------------------------
            // Core discovery / registry / resolver services
            // ---------------------------------------------------------
            services.TryAddSingleton<IRagOperationDiscoveryService, DefaultRagOperationDiscoveryService>();
            services.TryAddSingleton<IRagOperationResolver, DefaultRagOperationResolver>();

            // ---------------------------------------------------------
            // Dispatcher required by rag.retrieval step
            // ---------------------------------------------------------
            services.TryAddTransient<IRagRetrievalStepDispatcher, RagRetrievalStepDispatcher>();

            // ---------------------------------------------------------
            // Build descriptors immediately at registration time.
            // This keeps runtime lookup deterministic and avoids repeated reflection.
            // ---------------------------------------------------------
            var discoveryService = new DefaultRagOperationDiscoveryService();
            var descriptors = discoveryService.Discover(assemblyList);

            services.Replace(ServiceDescriptor.Singleton<IRagOperationRegistry>(
                _ => new DefaultRagOperationRegistry(descriptors)));

            // ---------------------------------------------------------
            // Register discovered concrete operation implementation types.
            // The resolver loads by descriptor.ImplementationType, so the
            // concrete type itself must exist in the container.
            // ---------------------------------------------------------
            foreach (var descriptor in descriptors)
            {
                services.TryAddTransient(descriptor.ImplementationType);

                if (typeof(IRagOperation).IsAssignableFrom(descriptor.ImplementationType))
                {
                    services.TryAddEnumerable(
                        ServiceDescriptor.Transient(typeof(IRagOperation), descriptor.ImplementationType));
                }
            }

            return services;
        }
    }
}