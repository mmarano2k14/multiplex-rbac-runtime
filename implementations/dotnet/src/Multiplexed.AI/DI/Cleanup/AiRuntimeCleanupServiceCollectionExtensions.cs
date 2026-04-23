using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;

namespace Multiplexed.AI.DI.Cleanup
{
    /// <summary>
    /// Registers AI execution cleanup services and options.
    /// </summary>
    public static class AiRuntimeCleanupServiceCollectionExtensions
    {
        /// <summary>
        /// Registers coordinated cleanup services for AI execution bundles.
        /// </summary>
        public static IServiceCollection AddAiExecutionCleanup(
            this IServiceCollection services,
            Action<AiExecutionCleanupOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddOptions<AiExecutionCleanupOptions>();

            if (configure is not null)
            {
                services.Configure(configure);
            }

            services.AddScoped<IAiExecutionCleanupService, AiExecutionCleanupService>();
            services.AddScoped<IAiDagDistributedStateCleanup, AiDagDistributedStateCleanup>();
            services.AddScoped<IAiOwnedRbacCleanupService, AiOwnedRbacCleanupService>();
            services.AddScoped<IAiExecutionSnapshotCleanupService, AiExecutionSnapshotCleanupService>();

            return services;
        }
    }
}