using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Observability.Tracing.Store;
using Multiplexed.AI.Runtime.Observability.Tracing.Stores.Mongo;

namespace Multiplexed.AI.Runtime.Observability.Tracing.Stores
{
    /// <summary>
    /// Creates the configured runtime trace store.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Centralizes trace store mode selection.
    /// - Allows tracing to support disabled, memory, MongoDB, and MemoryAndMongo modes.
    /// - Keeps DI registration simple and consistent with metrics.
    /// </remarks>
    internal static class AiRuntimeTraceStoreFactory
    {
        /// <summary>
        /// Creates the configured trace store.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>The configured trace store.</returns>
        public static IAiRuntimeTraceStore Create(
            IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var options = serviceProvider
                .GetRequiredService<IOptions<AiRuntimeTraceStoreOptions>>()
                .Value;

            return options.Mode switch
            {
                AiRuntimeTraceStoreMode.Disabled =>
                    serviceProvider.GetRequiredService<NoOpAiRuntimeTraceStore>(),

                AiRuntimeTraceStoreMode.Memory =>
                    serviceProvider.GetRequiredService<InMemoryAiRuntimeTraceStore>(),

                AiRuntimeTraceStoreMode.Mongo =>
                    serviceProvider.GetRequiredService<MongoAiRuntimeTraceStore>(),

                AiRuntimeTraceStoreMode.MemoryAndMongo =>
                    new CompositeAiRuntimeTraceStore(
                        serviceProvider.GetRequiredService<InMemoryAiRuntimeTraceStore>(),
                        serviceProvider.GetRequiredService<MongoAiRuntimeTraceStore>()),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.Mode),
                    options.Mode,
                    "Unsupported AI runtime trace store mode.")
            };
        }
    }
}