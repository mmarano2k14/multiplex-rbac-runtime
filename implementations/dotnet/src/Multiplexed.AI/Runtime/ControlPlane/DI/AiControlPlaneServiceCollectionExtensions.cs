using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.ControlPlane.Replay;
using Multiplexed.AI.Runtime.ControlPlane.Replay;

namespace Multiplexed.AI.Runtime.ControlPlane.DI
{
    /// <summary>
    /// Provides dependency injection registration for AI runtime control-plane services.
    /// </summary>
    public static class AiControlPlaneServiceCollectionExtensions
    {
        /// <summary>
        /// Registers AI runtime control-plane services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddAiControlPlane(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddOptions<AiReplayControlOptions>();

            services.AddSingleton<IAiReplayControlPlane, AiReplayControlPlane>();

            return services;
        }

        /// <summary>
        /// Registers AI runtime control-plane services with replay control-plane options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureReplay">Replay control-plane options configuration.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddAiControlPlane(
            this IServiceCollection services,
            Action<AiReplayControlOptions> configureReplay)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureReplay);

            services.Configure(configureReplay);

            services.AddSingleton<IAiReplayControlPlane, AiReplayControlPlane>();

            return services;
        }
    }
}