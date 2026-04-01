using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution;

namespace Multiplexed.AI.Runtime.DependencyInjection
{
    /// <summary>
    /// Registers AI execution engines.
    /// </summary>
    public static class AiRuntimeExecutionEngineServiceCollectionExtensions
    {
        public static IServiceCollection AddAiExecutionEngines(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddScoped<AiSequentialExecutionEngine>();
            services.AddScoped<AiDagExecutionEngine>();

            return services;
        }
    }
}