using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Multiplexed.Abstractions.AI.ControlPlane.Admission;
using Multiplexed.Abstractions.AI.ControlPlane.Execution;
using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.Replay;
using Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances;
using Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue;
using Multiplexed.AI.Runtime.ControlPlane.Admission;
using Multiplexed.AI.Runtime.ControlPlane.Execution;
using Multiplexed.AI.Runtime.ControlPlane.Observability;
using Multiplexed.AI.Runtime.ControlPlane.Replay;
using Multiplexed.AI.Runtime.ControlPlane.RuntimeInstances;
using Multiplexed.AI.Runtime.ControlPlane.RuntimeQueue;
using Multiplexed.AI.Runtime.ControlPlane.SharedController;
using Multiplexed.AI.Runtime.ControlPlane.SharedQueue;
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
        /// <param name="configureExecution">Optional execution control-plane options configuration.</param>
        /// <param name="configureRuntimeQueue">Optional local runtime queue control-plane options configuration.</param>
        /// <param name="configureRuntimeInstance">Optional runtime instance control-plane options configuration.</param>
        /// <param name="configureAdmission">Optional run admission options configuration.</param>
        /// <param name="configureSharedController">Optional shared runtime controller options configuration.</param>
        /// <param name="configureSharedQueue">Optional shared queue options configuration.</param>
        /// <param name="configureSharedQueuePump">Optional shared queue pump options configuration.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddAiControlPlane(
            this IServiceCollection services,
            Action<AiReplayControlOptions>? configureReplay = null,
            Action<AiExecutionControlPlaneOptions>? configureExecution = null,
            Action<AiRuntimeQueueControlPlaneOptions>? configureRuntimeQueue = null,
            Action<AiRuntimeInstanceControlPlaneOptions>? configureRuntimeInstance = null,
            Action<AiRunAdmissionOptions>? configureAdmission = null,
            Action<AiSharedRuntimeControllerOptions>? configureSharedController = null,
            Action<AiSharedQueueOptions>? configureSharedQueue = null,
            Action<AiSharedQueuePumpOptions>? configureSharedQueuePump = null)
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

            if (configureExecution is null)
            {
                services.AddOptions<AiExecutionControlPlaneOptions>();
            }
            else
            {
                services.Configure(configureExecution);
            }

            if (configureRuntimeQueue is null)
            {
                services.AddOptions<AiRuntimeQueueControlPlaneOptions>();
            }
            else
            {
                services.Configure(configureRuntimeQueue);
            }

            if (configureRuntimeInstance is null)
            {
                services.AddOptions<AiRuntimeInstanceControlPlaneOptions>();
            }
            else
            {
                services.Configure(configureRuntimeInstance);
            }

            if (configureAdmission is null)
            {
                services.AddOptions<AiRunAdmissionOptions>();
            }
            else
            {
                services.Configure(configureAdmission);
            }

            if (configureSharedController is null)
            {
                services.AddOptions<AiSharedRuntimeControllerOptions>();
            }
            else
            {
                services.Configure(configureSharedController);
            }

            if (configureSharedQueue is null)
            {
                services.AddOptions<AiSharedQueueOptions>();
            }
            else
            {
                services.Configure(configureSharedQueue);
            }

            if (configureSharedQueuePump is null)
            {
                services.AddOptions<AiSharedQueuePumpOptions>();
            }
            else
            {
                services.Configure(configureSharedQueuePump);
            }

            services.TryAddSingleton<IAiControlPlaneObserver, NoopAiControlPlaneObserver>();

            services.TryAddSingleton<IAiReplayControlPlane, AiReplayControlPlane>();
            services.TryAddSingleton<IAiExecutionControlPlane, AiExecutionControlPlane>();
            services.TryAddSingleton<IAiRuntimeQueueControlPlane, AiRuntimeQueueControlPlane>();

            services.TryAddSingleton<IAiRuntimeInstanceRegistry, InMemoryAiRuntimeInstanceRegistry>();
            services.TryAddSingleton<IAiRuntimeInstanceControlPlane, AiRuntimeInstanceControlPlane>();

            services.TryAddSingleton<IAiRunAdmissionController, AiRunAdmissionController>();

            services.TryAddSingleton<IAiSharedRunStore, InMemoryAiSharedRunStore>();
            services.TryAddSingleton<IAiSharedQueue, InMemoryAiSharedQueue>();
            services.TryAddSingleton<IAiSharedRunDispatcher, LocalAiSharedRunDispatcher>();
            services.TryAddSingleton<IAiSharedQueueDispatcher, AiSharedQueueDispatcher>();
            services.TryAddSingleton<IAiSharedQueuePump, AiSharedQueuePump>();
            services.TryAddSingleton<IAiSharedRuntimeController, AiSharedRuntimeController>();

            return services;
        }

        /// <summary>
        /// Registers the shared queue hosted background service.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional background service options configuration.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddAiSharedQueueBackgroundService(
            this IServiceCollection services,
            Action<AiSharedQueueBackgroundServiceOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (configure is null)
            {
                services.AddOptions<AiSharedQueueBackgroundServiceOptions>();
            }
            else
            {
                services.Configure(configure);
            }

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, AiSharedQueueBackgroundService>());

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