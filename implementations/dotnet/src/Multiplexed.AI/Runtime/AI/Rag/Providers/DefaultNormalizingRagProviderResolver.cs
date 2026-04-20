using System;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers
{
    /// <summary>
    /// Default implementation of <see cref="INormalizingRagProviderResolver"/>.
    ///
    /// PURPOSE:
    /// - Resolves normalizing RAG providers by their configured key.
    /// - Bridges provider discovery metadata with dependency injection runtime resolution.
    ///
    /// DESIGN:
    /// - Uses the provider registry as the source of truth for key-to-type mapping.
    /// - Uses <see cref="IServiceProvider"/> to resolve the actual provider instance.
    /// - Enforces that resolved instances implement <see cref="INormalizingRagProvider"/>.
    ///
    /// IMPORTANT:
    /// - This resolver does not create providers manually.
    /// - All provider instances must be registered in dependency injection.
    /// - Resolution must remain deterministic and fail fast on misconfiguration.
    /// </summary>
    public sealed class DefaultNormalizingRagProviderResolver : INormalizingRagProviderResolver
    {
        private readonly IRagProviderRegistry _providerRegistry;
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultNormalizingRagProviderResolver"/> class.
        /// </summary>
        /// <param name="providerRegistry">
        /// The registry containing discovered RAG provider descriptors.
        /// </param>
        /// <param name="services">
        /// The application service provider used to resolve provider instances.
        /// </param>
        public DefaultNormalizingRagProviderResolver(
            IRagProviderRegistry providerRegistry,
            IServiceProvider services)
        {
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Resolves a normalizing RAG provider by key.
        ///
        /// FLOW:
        /// 1. Validate input key
        /// 2. Lookup provider descriptor from registry
        /// 3. Resolve implementation type from dependency injection
        /// 4. Validate resolved instance
        /// 5. Return normalized provider instance
        ///
        /// IMPORTANT:
        /// - Fails fast if the key is unknown.
        /// - Fails fast if the implementation is not registered in DI.
        /// - Fails fast if the resolved service does not implement the expected contract.
        /// </summary>
        /// <param name="providerKey">
        /// The configured provider key.
        /// </param>
        /// <returns>
        /// The resolved <see cref="INormalizingRagProvider"/> instance.
        /// </returns>
        public INormalizingRagProvider Resolve(string providerKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

            RagProviderDescriptor descriptor = _providerRegistry.GetByKey(providerKey);

            object? service = _services.GetService(descriptor.ImplementationType);

            if (service is null)
            {
                throw new InvalidOperationException(
                    $"The RAG provider '{providerKey}' is registered in discovery but its implementation type " +
                    $"'{descriptor.ImplementationType.FullName}' is not registered in dependency injection.");
            }

            if (service is not INormalizingRagProvider provider)
            {
                throw new InvalidOperationException(
                    $"The resolved service for RAG provider '{providerKey}' does not implement " +
                    $"'{typeof(INormalizingRagProvider).FullName}'.");
            }

            return provider;
        }
    }
}