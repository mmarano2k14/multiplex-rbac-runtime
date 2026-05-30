using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;
using Multiplexed.AI.Runtime.AI.Rag.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Discovery;
using Multiplexed.AI.Runtime.AI.Rag.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.AI.Rag.Operations.Discovery;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors.Postgres;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors.SqlServer;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Readers;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Resolvers;
using Multiplexed.AI.Runtime.AI.Rag.Retrieval;
using Multiplexed.AI.Runtime.AI.Rag.Steps;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Observability.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Multiplexed.AI.Runtime.AI.Rag.DI
{
    /// <summary>
    /// Provides dependency injection helpers for the RAG runtime subsystem.
    ///
    /// PURPOSE:
    /// - Registers the core RAG services required by runtime execution.
    /// - Registers discovery, registries, resolvers, and reusable runtime services.
    /// - Supports dynamic registration from attributed assemblies.
    ///
    /// DESIGN:
    /// - <see cref="AddRagCore(IServiceCollection)"/> registers the stable runtime foundation.
    /// - <see cref="AddRagFromAssemblies(IServiceCollection, Assembly[])"/> adds
    ///   discovery-based registration on top of that foundation.
    ///
    /// IMPORTANT:
    /// - Providers, operations, retrievals, and composers discovered from assemblies are
    ///   registered by their concrete implementation type.
    /// - Resolvers rely on registries + <see cref="IServiceProvider"/>.
    /// </summary>
    public static class RagServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the core RAG runtime services.
        ///
        /// PURPOSE:
        /// - Registers the stable runtime foundation required by the RAG subsystem.
        /// - Adds discovery support, registries, resolvers, merger, and step types.
        ///
        /// IMPORTANT:
        /// - This method does not scan assemblies by itself.
        /// - Use <see cref="AddRagFromAssemblies(IServiceCollection, Assembly[])"/>
        ///   to register attributed providers, operations, retrievals, and composers dynamically.
        /// </summary>
        public static IServiceCollection AddRagCore(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // -----------------------------------------------------------------
            // Discovery
            // -----------------------------------------------------------------
            services.TryAddSingleton<IRagDiscoveryService, DefaultRagDiscoveryService>();
            services.TryAddSingleton<IRagOperationDiscoveryService, DefaultRagOperationDiscoveryService>();

            // -----------------------------------------------------------------
            // Registries
            // NOTE:
            // These default empty registries allow the runtime to start even
            // before discovery-based registration is added.
            // -----------------------------------------------------------------
            services.TryAddSingleton<IRagProviderRegistry>(_ =>
                new DefaultRagProviderRegistry(Array.Empty<RagProviderDescriptor>()));

            services.TryAddSingleton<IRagOperationRegistry>(_ =>
                new DefaultRagOperationRegistry(Array.Empty<RagOperationDescriptor>()));

            services.TryAddSingleton<IRagRetrievalRegistry>(_ =>
                new DefaultRagRetrievalRegistry(Array.Empty<RagRetrievalDescriptor>()));

            services.TryAddSingleton<IRagComposerRegistry>(_ =>
                new DefaultRagComposerRegistry(Array.Empty<RagComposerDescriptor>()));

            // -----------------------------------------------------------------
            // Core runtime configuration / services
            // -----------------------------------------------------------------
            services.TryAddSingleton(new RagMultiProviderRetrievalOptions());

            services.TryAddSingleton<IRagBatchMerger, DefaultRagBatchMerger>();

            // -----------------------------------------------------------------
            // Resolvers
            // -----------------------------------------------------------------
            services.TryAddSingleton<INormalizingRagProviderResolver, DefaultNormalizingRagProviderResolver>();
            services.TryAddSingleton<IRagOperationResolver, DefaultRagOperationResolver>();
            services.TryAddSingleton<IRagRetrievalResolver, DefaultRagRetrievalResolver>();
            services.TryAddSingleton<IRagComposerResolver, DefaultRagComposerResolver>();

            // -----------------------------------------------------------------
            // RAG logging
            // -----------------------------------------------------------------
            services.TryAddSingleton<IAiRagRetrievalLogger, AiRagRetrievalLogger>();
            services.TryAddSingleton<IAiRagCompositionLogger, AiRagCompositionLogger>();
            services.TryAddSingleton<IAiRagLogger, AiRagLogger>();

            // -----------------------------------------------------------------
            // Normalization / dispatch
            // -----------------------------------------------------------------
            services.TryAddTransient<IRagStepResultNormalizer, DefaultRagStepResultNormalizer>();
            services.TryAddTransient<IRagRetrievalStepDispatcher, RagRetrievalStepDispatcher>();

            // -----------------------------------------------------------------
            // RAG Readers and Connectors
            // -----------------------------------------------------------------

            services.AddTransient<IRelationalRagConnectorResolver, DefaultRelationalRagConnectorResolver>();
            services.AddTransient<IRelationalRagRecordReader, DefaultRelationalRagRecordReader>();

            services.AddTransient<IRelationalRagConnector, SqlServerRelationalRagConnector>();
            services.AddTransient<IRelationalRagConnector, PostgresRelationalRagConnector>();

            services.AddSingleton<IRelationalRagConnectorResolver, DefaultRelationalRagConnectorResolver>();
            services.AddTransient<IRelationalRagRecordReader, DefaultRelationalRagRecordReader>();

            // -----------------------------------------------------------------
            // Steps
            // Register step types explicitly so step discovery / resolution can
            // obtain them from DI.
            // -----------------------------------------------------------------

            /*
            services.TryAddTransient<RagVectorStep>();
            services.TryAddTransient<RagSqlStep>();
            services.TryAddTransient<RagRuntimeStep>();
            services.TryAddTransient<RagMergeStep>();
            services.TryAddTransient<RagComposeStep>();
            services.TryAddTransient<RagMultiStep>();
            */

            return services;
        }

        /// <summary>
        /// Adds discovery-based RAG registration from the specified assemblies.
        ///
        /// PURPOSE:
        /// - Scans assemblies for attributed RAG providers, operations, retrievals, and composers.
        /// - Builds and replaces the runtime registries with discovered descriptors.
        /// - Registers discovered implementation types in dependency injection.
        ///
        /// FLOW:
        /// 1. Ensure core RAG services are registered
        /// 2. Discover providers, operations, retrievals, and composers
        /// 3. Register descriptors into registries
        /// 4. Register implementation types in DI
        ///
        /// IMPORTANT:
        /// - Concrete implementation types are registered directly.
        /// - Resolvers depend on these concrete registrations.
        /// - If called multiple times, the latest discovered registries replace
        ///   the previous ones.
        /// </summary>
        public static IServiceCollection AddRagFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assemblies);

            services.AddRagCore();

            var validAssemblies = assemblies
                .Where(x => x is not null)
                .Distinct()
                .ToArray();

            if (validAssemblies.Length == 0)
            {
                throw new ArgumentException(
                    "At least one assembly must be provided for RAG discovery.",
                    nameof(assemblies));
            }

            // Use a temporary discovery service instance. This avoids needing a
            // built service provider during registration.
            var discovery = new DefaultRagDiscoveryService();
            var operationDiscovery = new DefaultRagOperationDiscoveryService();

            var providerDescriptors = discovery.DiscoverProviders(validAssemblies);
            var operationDescriptors = operationDiscovery.Discover(validAssemblies);
            var retrievalDescriptors = discovery.DiscoverRetrievals(validAssemblies);
            var composerDescriptors = discovery.DiscoverComposers(validAssemblies);

            // Replace default empty registries with discovered registries.
            services.Replace(ServiceDescriptor.Singleton<IRagProviderRegistry>(
                _ => new DefaultRagProviderRegistry(providerDescriptors)));

            services.Replace(ServiceDescriptor.Singleton<IRagOperationRegistry>(
                _ => new DefaultRagOperationRegistry(operationDescriptors)));

            services.Replace(ServiceDescriptor.Singleton<IRagRetrievalRegistry>(
                _ => new DefaultRagRetrievalRegistry(retrievalDescriptors)));

            services.Replace(ServiceDescriptor.Singleton<IRagComposerRegistry>(
                _ => new DefaultRagComposerRegistry(composerDescriptors)));

            // Register discovered implementation types by concrete type.
            RegisterDiscoveredImplementationTypes(
                services,
                providerDescriptors.Select(x => x.ImplementationType));

            RegisterDiscoveredImplementationTypes(
                services,
                operationDescriptors.Select(x => x.ImplementationType));

            RegisterDiscoveredImplementationTypes(
                services,
                retrievalDescriptors.Select(x => x.ImplementationType));

            RegisterDiscoveredImplementationTypes(
                services,
                composerDescriptors.Select(x => x.ImplementationType));

            return services;
        }

        /// <summary>
        /// Registers the specified implementation types in DI by their concrete type.
        ///
        /// PURPOSE:
        /// - Supports resolver-based activation by <see cref="Type"/>.
        /// - Keeps registration deterministic and idempotent.
        ///
        /// IMPORTANT:
        /// - Types are registered as transient by default.
        /// - This is appropriate for most runtime components unless a specific
        ///   component later requires a different lifetime.
        /// </summary>
        private static void RegisterDiscoveredImplementationTypes(
            IServiceCollection services,
            IEnumerable<Type> implementationTypes)
        {
            foreach (var implementationType in implementationTypes
                         .Where(x => x is not null)
                         .Distinct())
            {
                services.TryAddTransient(implementationType);
            }
        }
    }
}