using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.Replay;
using Multiplexed.AI.Runtime.ControlPlane.Observability;
using Multiplexed.AI.Runtime.ControlPlane.Replay;
using Multiplexed.AI.Runtime.Observability.Logging;

namespace Multiplexed.AI.Runtime.ControlPlane.DI
{
    /// <summary>
    /// Provides dependency injection registration for AI runtime control-plane services.
    /// </summary>
    public static class AiControlPlaneServiceCollectionExtensions
    {
        /// <summary>
        /// Registers AI runtime control-plane services.
        ///
        /// By default, a no-op control-plane observer is registered so the runtime
        /// can operate without logging, metrics, tracing, or ledger exporters.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureReplay">Optional replay control-plane options configuration.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddAiControlPlane(
            this IServiceCollection services,
            Action<AiReplayControlOptions>? configureReplay = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (configureReplay is null)
            {
                services.AddOptions<AiReplayControlOptions>();
            }
            else
            {
                services.Configure(configureReplay);
            }

            services.TryAddSingleton<IAiControlPlaneObserver, NoopAiControlPlaneObserver>();
            services.TryAddSingleton<IAiReplayControlPlane, AiReplayControlPlane>();

            return services;
        }

        /// <summary>
        /// Enables structured logging for AI control-plane events.
        ///
        /// This replaces the default no-op observer with a logging observer
        /// that forwards control-plane events to the runtime logging layer.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddAiControlPlaneLogging(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IAiControlPlaneLogger, AiControlPlaneLogger>();

            services.RemoveAll<IAiControlPlaneObserver>();
            services.AddSingleton<IAiControlPlaneObserver, LoggedAiControlPlaneObserver>();

            return services;
        }
    }
}