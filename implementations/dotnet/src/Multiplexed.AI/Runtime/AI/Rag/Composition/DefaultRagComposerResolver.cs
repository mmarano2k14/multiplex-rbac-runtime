using System;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Composition
{
    /// <summary>
    /// Default implementation of <see cref="IRagComposerResolver"/>.
    ///
    /// PURPOSE:
    /// - Resolves composers dynamically from configuration.
    /// - Bridges descriptor registry and dependency injection.
    ///
    /// IMPORTANT:
    /// - Composer implementations must be registered in DI.
    /// </summary>
    public sealed class DefaultRagComposerResolver : IRagComposerResolver
    {
        private readonly IRagComposerRegistry _registry;
        private readonly IServiceProvider _services;

        public DefaultRagComposerResolver(
            IRagComposerRegistry registry,
            IServiceProvider services)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IRagComposer<RagStructuredContext> Resolve(string composerKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(composerKey);

            RagComposerDescriptor descriptor = _registry.GetByKey(composerKey);

            object? service = _services.GetService(descriptor.ImplementationType);

            if (service is null)
            {
                throw new InvalidOperationException(
                    $"RAG composer '{composerKey}' is registered but its implementation " +
                    $"'{descriptor.ImplementationType.FullName}' is not registered in DI.");
            }

            if (service is not IRagComposer<RagStructuredContext> composer)
            {
                throw new InvalidOperationException(
                    $"Resolved composer '{composerKey}' does not implement IRagComposer<RagStructuredContext>.");
            }

            return composer;
        }
    }
}