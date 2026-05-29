using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay.Fingerprint;

namespace Multiplexed.AI.DI.Persistence
{
    /// <summary>
    /// Registers AI execution replay services used to restore runtime state,
    /// generate replay fingerprints, and persist replay metadata.
    /// </summary>
    public static class AiExecutionReplayExtension
    {
        /// <summary>
        /// Registers the default AI execution replay services.
        /// </summary>
        public static IServiceCollection AddAiExecutionReplay<TContextSnapshot>(
            this IServiceCollection services)
            where TContextSnapshot : class
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddScoped<
                IAiExecutionReplayService,
                DefaultAiExecutionReplayService<TContextSnapshot>>();

            services.TryAddSingleton<
                IAiExecutionReplayFingerprintBuilder,
                DefaultAiExecutionReplayFingerprintBuilder>();

            services.TryAddSingleton<
                IAiExecutionReplayMetadataService,
                DefaultAiExecutionReplayMetadataService>();

            services.TryAddSingleton<
                IAiExecutionReplayMetadataStore,
                InMemoryAiExecutionReplayMetadataStore>();

            return services;
        }

        /// <summary>
        /// Registers the default AI execution replay services for the standard
        /// execution context snapshot type used by the runtime.
        /// </summary>
        public static IServiceCollection AddAiExecutionReplay(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddAiExecutionReplay<ExecutionContextSnapshot>();
        }
    }
}