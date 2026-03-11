// ============================================================================
// MultiplexedRbac.Runtime.NServiceBus - DI Extensions
// Usage:
// builder.Services.AddMultiplexedRbacNServiceBus();
//
// Then in endpoint config (API/Worker):
// endpointConfig.Pipeline.Register(typeof(OutgoingExecutionContextHeaderBehavior), "...");
// endpointConfig.Pipeline.Register(typeof(IncomingExecutionContextRehydrateBehavior), "...");
// ============================================================================

using Microsoft.Extensions.DependencyInjection;

namespace MultiplexedRbac.Runtime.Messaging.NServiceBus.DI
{
    public static class NServiceBusServiceCollectionExtensions
    {
        /// <summary>
        /// Registers NServiceBus-only runtime components (pipeline behaviors).
        /// They reuse:
        /// - IExecutionContextAccessor
        /// - IContextStore
        /// - IOptions&lt;ContextRuntimeOptions&gt;
        /// which are provided by AddMultiplexedRbacRuntime(...).
        /// </summary>
        public static IServiceCollection AddMultiplexedRbacNServiceBus(this IServiceCollection services)
        {
            services.AddTransient<OutgoingExecutionContextHeaderBehavior>();
            services.AddTransient<IncomingExecutionContextRehydrateBehavior>();
            return services;
        }
    }
}