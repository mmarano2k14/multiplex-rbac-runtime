

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;

namespace Multiplexed.AI.DI.Persistence
{
    /// <summary>
    /// Registers execution replay services used to restore runtime state
    /// from persisted execution snapshots.
    ///
    /// Purpose:
    /// - Centralize replay-related dependency injection
    /// - Keep fixture and host registration clean
    /// - Allow production and integration tests to use the same registration path
    ///
    /// Design:
    /// - Uses TryAdd so callers may override defaults if needed
    /// - Registers replay preparation separately to keep replay orchestration focused
    /// - Generic on snapshot context type to match snapshot store contracts
    /// </summary>
    public static class AiExecutionReplayExtension
    {
        /// <summary>
        /// Registers the default AI execution replay services.
        /// </summary>
        /// <typeparam name="TContextSnapshot">
        /// The external context snapshot type stored inside execution snapshots.
        /// </typeparam>
        /// <param name="services">
        /// The service collection.
        /// </param>
        /// <returns>
        /// The same service collection for fluent chaining.
        /// </returns>
        public static IServiceCollection AddAiExecutionReplay<TContextSnapshot>(
            this IServiceCollection services)
            where TContextSnapshot : class
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddScoped<
                IAiExecutionReplayService,
                DefaultAiExecutionReplayService<TContextSnapshot>>();

            return services;
        }

        /// <summary>
        /// Registers the default AI execution replay services for the standard
        /// execution context snapshot type used by the runtime.
        /// </summary>
        /// <param name="services">
        /// The service collection.
        /// </param>
        /// <returns>
        /// The same service collection for fluent chaining.
        /// </returns>
        public static IServiceCollection AddAiExecutionReplay(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddAiExecutionReplay<ExecutionContextSnapshot>();
        }
    }
}