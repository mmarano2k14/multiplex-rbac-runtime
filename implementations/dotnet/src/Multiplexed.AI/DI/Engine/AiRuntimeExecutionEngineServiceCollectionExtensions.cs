using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution.Engine;

namespace Multiplexed.AI.DI.Engine
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