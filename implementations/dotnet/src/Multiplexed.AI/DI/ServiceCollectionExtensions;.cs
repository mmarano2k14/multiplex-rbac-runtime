using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI;
using Multiplexed.AI.Abstractions;
using Multiplexed.AI.Providers;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Steps;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Stores.Memory;

namespace Multiplexed.AI.DI
{
    /// <summary>
    /// Registers AI runtime services, providers, pipeline services, and execution engine components.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the AI runtime module to the service collection.
        /// </summary>
        /// <param name="services">Target service collection.</param>
        /// <returns>The same service collection instance.</returns>
        public static IServiceCollection AddMultiplexAI(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Provider / service abstraction
            services.AddSingleton<IAIProvider, FakeAIProvider>();
            services.AddSingleton<IAIService, AIService>();

            // Pipeline runtime
            services.AddScoped<AiStepRunner>();
            services.AddScoped<AiPipelineService>();

            // Execution runtime
            services.AddScoped<AiExecutionEngine>();

            // Steps
            services.AddScoped<IAiStep, SummaryStep>();

            // Stores
            services.AddSingleton<MemoryAiExecutionStore>();
            services.AddSingleton<RedisAiExecutionStore>();
            services.AddSingleton<IAiExecutionStore, AiExecutionStore>();

            return services;
        }
    }
}