using System;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;

namespace Multiplexed.AI.Runtime.AI.Rag.Retrieval
{
    /// <summary>
    /// Default implementation of <see cref="IRagRetrievalResolver"/>.
    ///
    /// PURPOSE:
    /// - Resolves retrieval strategies dynamically from configuration.
    /// - Bridges descriptor registry and dependency injection.
    ///
    /// DESIGN:
    /// - Uses registry as source of truth.
    /// - Uses DI to instantiate retrieval implementations.
    /// - Enforces type safety.
    ///
    /// IMPORTANT:
    /// - Retrievals must be registered in DI.
    /// - Resolution is deterministic and fail-fast.
    /// </summary>
    public sealed class DefaultRagRetrievalResolver : IRagRetrievalResolver
    {
        private readonly IRagRetrievalRegistry _registry;
        private readonly IServiceProvider _services;

        public DefaultRagRetrievalResolver(
            IRagRetrievalRegistry registry,
            IServiceProvider services)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IRagRetrieval Resolve(string retrievalKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retrievalKey);

            RagRetrievalDescriptor descriptor = _registry.GetByKey(retrievalKey);

            object? service = _services.GetService(descriptor.ImplementationType);

            if (service is null)
            {
                throw new InvalidOperationException(
                    $"RAG retrieval '{retrievalKey}' is registered but its implementation " +
                    $"'{descriptor.ImplementationType.FullName}' is not registered in DI.");
            }

            if (service is not IRagRetrieval retrieval)
            {
                throw new InvalidOperationException(
                    $"Resolved retrieval '{retrievalKey}' does not implement IRagRetrieval.");
            }

            return retrieval;
        }
    }
}