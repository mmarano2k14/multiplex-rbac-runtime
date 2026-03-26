using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions;
using Multiplexed.AI.Providers;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Registry;
using Multiplexed.AI.Runtime.Pipeline.Retry;
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

            // ------------------------------------------------------------
            // Retry / step execution infrastructure
            // ------------------------------------------------------------
            services.AddSingleton<IAiRetryExceptionClassifier, DefaultAiRetryExceptionClassifier>();
            services.AddScoped<IAiStepExecutor, AiStepExecutor>();

            // ------------------------------------------------------------
            // Provider / service abstraction
            // ------------------------------------------------------------
            services.AddScoped<IAiProvider, FakeAIProvider>();
            services.AddScoped<IAiService, AiService>();

            // ------------------------------------------------------------
            // Pipeline definition / resolution / execution
            // ------------------------------------------------------------
            services.AddScoped<IAiPipelineDefinitionProvider, InMemoryAiPipelineDefinitionProvider>();
            services.AddScoped<IAiStepRegistry, InMemoryAiStepRegistry>();
            services.AddScoped<IAiPipelineResolver, AiPipelineResolver>();
            services.AddScoped<IAiPipelineExecutor, AiPipelineExecutor>();

            // ------------------------------------------------------------
            // Execution runtime
            // ------------------------------------------------------------
            services.AddScoped<IAiExecutionEngine, AiExecutionEngine>();

            // ------------------------------------------------------------
            // Steps
            // ------------------------------------------------------------
            services.AddScoped<SummaryStep>();
            services.AddScoped<IAiStep>(sp => sp.GetRequiredService<SummaryStep>());

            // ------------------------------------------------------------
            // Stores
            // ------------------------------------------------------------
            services.AddSingleton<MemoryAiExecutionStore>();
            services.AddSingleton<RedisAiExecutionStore>();
            services.AddSingleton<IAiExecutionStore, AiExecutionStore>();

            // ------------------------------------------------------------
            // Logger
            // ------------------------------------------------------------
            services.AddScoped<IAiExecutionEngineLogger, AiExecutionEngineLogger>();
            services.AddScoped<IAiPipelineLogger, AiPipelineLogger>();
            services.AddScoped<IAiStepExecutorLogger, AiStepExecutorLogger>();
            services.AddScoped<IAiPipelineServiceLogger, AiPipelineServiceLogger>(); // TO REMOVE
            services.AddScoped<IAiRuntimeLogger, AiRuntimeLogger>();

            return services;
        }
    }
}