using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Observability.Metrics.Store;
using Multiplexed.AI.Runtime.Observability.Metrics.Stores.Mongo;

namespace Multiplexed.AI.Runtime.Observability.Metrics.Stores
{
    /// <summary>
    /// Creates the configured runtime metric store implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The factory centralizes metric store selection so dependency injection registration
    /// remains simple and the runtime can support disabled, memory, MongoDB, or combined
    /// memory and MongoDB storage modes.
    /// </para>
    ///
    /// <para>
    /// The in-memory store can be kept available as a concrete singleton for live
    /// diagnostics, dashboards, tests, and local runtime inspection.
    /// </para>
    /// </remarks>
    internal static class AiRuntimeMetricStoreFactory
    {
        /// <summary>
        /// Creates the configured runtime metric store.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>The configured runtime metric store.</returns>
        public static IAiRuntimeMetricStore Create(
            IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var options = serviceProvider
                .GetRequiredService<IOptions<AiRuntimeMetricStoreOptions>>()
                .Value;

            return options.Mode switch
            {
                AiRuntimeMetricStoreMode.Disabled =>
                    serviceProvider.GetRequiredService<NoOpAiRuntimeMetricStore>(),

                AiRuntimeMetricStoreMode.Memory =>
                    serviceProvider.GetRequiredService<InMemoryAiRuntimeMetricStore>(),

                AiRuntimeMetricStoreMode.Mongo =>
                    serviceProvider.GetRequiredService<MongoAiRuntimeMetricStore>(),

                AiRuntimeMetricStoreMode.MemoryAndMongo =>
                    new CompositeAiRuntimeMetricStore(
                        new IAiRuntimeMetricStore[]
                        {
                            serviceProvider.GetRequiredService<InMemoryAiRuntimeMetricStore>(),
                            serviceProvider.GetRequiredService<MongoAiRuntimeMetricStore>()
                        }),

                _ =>
                    serviceProvider.GetRequiredService<InMemoryAiRuntimeMetricStore>()
            };
        }
    }
}