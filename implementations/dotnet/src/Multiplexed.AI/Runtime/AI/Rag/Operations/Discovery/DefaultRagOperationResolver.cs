using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;

namespace Multiplexed.AI.Runtime.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Default DI-backed RAG operation resolver.
    ///
    /// Uses:
    /// - <see cref="IRagOperationRegistry"/> for deterministic metadata lookup
    /// - <see cref="IServiceProvider"/> for instance creation/resolution
    /// </summary>
    public sealed class DefaultRagOperationResolver : IRagOperationResolver
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IRagOperationRegistry _registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRagOperationResolver"/> class.
        /// </summary>
        public DefaultRagOperationResolver(
            IServiceProvider serviceProvider,
            IRagOperationRegistry registry)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <inheritdoc />
        public IRagOperation Resolve(string key)
        {
            var descriptor = _registry.Get(key);

            var service = _serviceProvider.GetService(descriptor.ImplementationType);
            if (service is not IRagOperation operation)
            {
                throw new InvalidOperationException(
                    $"RAG operation '{key}' with implementation type '{descriptor.ImplementationType.FullName}' " +
                    "could not be resolved from the service provider.");
            }

            return operation;
        }
    }
}