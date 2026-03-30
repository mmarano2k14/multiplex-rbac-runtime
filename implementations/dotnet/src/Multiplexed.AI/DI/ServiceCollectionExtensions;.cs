using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Providers;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
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
        /// <param name="configuration">Application configuration.</param>
        /// <returns>The same service collection instance.</returns>
        public static IServiceCollection AddMultiplexAI(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // ------------------------------------------------------------
            // Options
            // ------------------------------------------------------------
            services.Configure<AiEngineOptions>(
                configuration.GetSection("AiEngine"));

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
            // Step discovery / registry
            // ------------------------------------------------------------
            services.AddAiStepsFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly);

            // ------------------------------------------------------------
            // Pipeline definition / resolution / execution
            // ------------------------------------------------------------
            services.AddScoped<InMemoryAiPipelineDefinitionProvider>();
            services.AddScoped<JsonAiPipelineDefinitionProvider>(sp =>
            {
                var options = sp.GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<AiEngineOptions>>().Value;

                if (string.IsNullOrWhiteSpace(options.JsonPipelineDefinitionFilePath))
                {
                    throw new InvalidOperationException(
                        "AiEngine:JsonPipelineDefinitionFilePath must be configured when using the Json pipeline definition source.");
                }

                return new JsonAiPipelineDefinitionProvider(
                    options.JsonPipelineDefinitionFilePath);
            });


            services.AddScoped<IAiPipelineDefinitionSourceSelector, DefaultAiPipelineDefinitionSourceSelector>();
            services.AddScoped<IAiPipelineResolver, AiPipelineResolver>();
            services.AddScoped<IAiSequentialPipelineExecutor, AiSequentialPipelineExecutor>();

            // ------------------------------------------------------------
            // Execution runtime
            // ------------------------------------------------------------
            services.AddScoped<IAiExecutionEngine, AiSequentialExecutionEngine>();

            // ------------------------------------------------------------
            // Stores
            // ------------------------------------------------------------
            services.AddSingleton<MemoryAiExecutionStore>();
            services.AddSingleton<RedisAiExecutionStore>();
            services.AddSingleton<IAiDagExecutionStore, RedisAiDagExecutionStore>();
            services.AddSingleton<IAiExecutionStore, AiExecutionStore>();

            // ------------------------------------------------------------
            // Logger
            // ------------------------------------------------------------
            services.AddScoped<IAiExecutionEngineLogger, AiExecutionEngineLogger>();
            services.AddScoped<IAiPipelineLogger, AiPipelineLogger>();
            services.AddScoped<IAiStepExecutorLogger, AiStepExecutorLogger>();
            services.AddScoped<IAiPipelineLogger, AiPipelineLogger>();
            services.AddScoped<IAiRuntimeLogger, AiRuntimeLogger>();

            return services;
        }
    }
}