using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Memory;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Abstractions;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Providers;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Memory;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.Realtime.Context;

namespace Multiplexed.AI.DI
{
    /// <summary>
    /// Registers AI runtime services, providers, pipeline services, and execution engine components.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the AI runtime module to the service collection from application configuration.
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

            var aiEngineOptions = new AiEngineOptions();
            configuration.GetSection("AiEngine").Bind(aiEngineOptions);

            var cleanupOptions = new AiExecutionCleanupOptions();
            configuration.GetSection("AiExecutionCleanup").Bind(cleanupOptions);

            aiEngineOptions.Cleanup = cleanupOptions;

            return services.AddMultiplexAI(aiEngineOptions);
        }

        /// <summary>
        /// Adds the AI runtime module to the service collection from strongly typed options.
        /// </summary>
        /// <param name="services">Target service collection.</param>
        /// <param name="options">Strongly typed AI engine options.</param>
        /// <returns>The same service collection instance.</returns>
        public static IServiceCollection AddMultiplexAI(
            this IServiceCollection services,
            AiEngineOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            // ------------------------------------------------------------
            // Execution State retention policy
            // ------------------------------------------------------------
            services.TryAddSingleton<IAiExecutionStateRetentionPolicy>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AiEngineOptions>>().Value;
                return new DefaultAiExecutionStateRetentionPolicy(options.StateRetention);
            });

            // ------------------------------------------------------------
            // Options
            // ------------------------------------------------------------
            services.AddSingleton<IOptions<AiEngineOptions>>(Options.Create(options));
            services.AddSingleton<IOptions<AiExecutionCleanupOptions>>(
                Options.Create(options.Cleanup ?? new AiExecutionCleanupOptions()));

            // ------------------------------------------------------------
            // Payload store : policies and resolvers
            // ------------------------------------------------------------

            services.Configure<AiPayloadStoreOptions>(opts =>
            {
                opts.Enabled = options.PayloadStore.Enabled;
                opts.Provider = options.PayloadStore.Provider;
                opts.RequireReplaySafePayloads = options.PayloadStore.RequireReplaySafePayloads;
                opts.MaxInlineSizeBytes = options.PayloadStore.MaxInlineSizeBytes;

                opts.Mongo = options.PayloadStore.Mongo;
                opts.RedisCache = options.PayloadStore.RedisCache;
            });

            // Concrete stores (NE PAS exposer IAiPayloadStore ici)

            services.TryAddSingleton<IAiPayloadMetrics, InMemoryAiPayloadMetrics>();
            services.TryAddSingleton<InMemoryAiPayloadStore>();
            services.TryAddSingleton<MongoAiPayloadStore>();
            services.TryAddSingleton<RedisAiPayloadStore>();
            services.TryAddSingleton<MongoRedisCachedAiPayloadStore>();

           
            services.TryAddSingleton<IAiStepResultPayloadCompactor, DefaultAiStepResultPayloadCompactor>();

            // Resolver (point d’entrée UNIQUE)
            services.TryAddSingleton<IAiPayloadStoreResolver, DefaultAiPayloadStoreResolver>();


            // Payload policy + resolver
            services.TryAddSingleton<IAiExecutionDataPolicy, SmartInlineAiExecutionDataPolicy>();
            services.TryAddSingleton<IAiExecutionPayloadResolver, DefaultAiExecutionPayloadResolver>();


            // ------------------------------------------------------------
            // Memory system
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiMemoryScoringPolicy, DefaultAiMemoryScoringPolicy>();
            services.TryAddSingleton<IAiMemoryLifecyclePolicy, DefaultAiMemoryLifecyclePolicy>();
            services.TryAddSingleton<IAiConsolidatedMemoryStore, InMemoryAiConsolidatedMemoryStore>();
            services.TryAddSingleton<IAiMemoryLifecycleEngine, DefaultAiMemoryLifecycleEngine>();
            services.TryAddSingleton<IAiMemoryWriter, DefaultAiMemoryWriter>();

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
                var resolvedOptions = sp.GetRequiredService<IOptions<AiEngineOptions>>().Value;

                if (string.IsNullOrWhiteSpace(resolvedOptions.JsonPipelineDefinitionFilePath))
                {
                    throw new InvalidOperationException(
                        "AiEngineOptions.JsonPipelineDefinitionFilePath must be configured when using the Json pipeline definition source.");
                }

                return new JsonAiPipelineDefinitionProvider(
                    resolvedOptions.JsonPipelineDefinitionFilePath);
            });

            services.AddScoped<IAiPipelineDefinitionSourceSelector, DefaultAiPipelineDefinitionSourceSelector>();
            services.AddScoped<IAiPipelineResolver, AiPipelineResolver>();
            services.AddScoped<IAiSequentialPipelineExecutor, AiSequentialPipelineExecutor>();

            // ------------------------------------------------------------
            // Stores
            // ------------------------------------------------------------
            services.AddSingleton<MemoryAiExecutionStore>();
            services.AddSingleton<RedisAiExecutionStore>();
            services.AddSingleton<IAiDagExecutionStore, RedisAiDagExecutionStore>();
            services.AddSingleton<IAiExecutionStore, AiExecutionStore>();
            services.AddSingleton<IAiExecutionKeyBuilder, AiExecutionKeyBuilder>();

            // ------------------------------------------------------------
            // Cleanup
            // ------------------------------------------------------------
            services.AddScoped<IAiExecutionCleanupService, AiExecutionCleanupService>();
            services.AddScoped<IAiDagDistributedStateCleanup, AiDagDistributedStateCleanup>();
            services.TryAddSingleton<IAiOwnedResourceLocator, NoopAiOwnedResourceLocator>();
            services.TryAddSingleton<IAiOwnedResourceDeleter, NoopAiOwnedResourceDeleter>();
            services.AddScoped<IAiOwnedRbacCleanupService, AiOwnedRbacCleanupService>();
            services.AddScoped<IAiExecutionSnapshotCleanupService, AiExecutionSnapshotCleanupService>();
            services.AddScoped<IAiOwnedRbacCleanupService, NoOpAiOwnedRbacCleanupService>();

            // ------------------------------------------------------------
            // Logger
            // ------------------------------------------------------------
            services.AddScoped<IRuntimeEventContext, RealtimeEventContext>();
            services.AddScoped<IAiExecutionEngineLogger, AiExecutionEngineLogger>();
            services.AddScoped<IAiPipelineLogger, AiPipelineLogger>();
            services.AddScoped<IAiPipelineServiceLogger, AiPipelineServiceLogger>();
            services.AddScoped<IAiStepExecutorLogger, AiStepExecutorLogger>();
            services.AddScoped<IAiRuntimeLogger, AiRuntimeLogger>();
            services.AddScoped<IAiRagCompositionLogger, AiRagCompositionLogger>();
            services.AddScoped<IAiRagRetrievalLogger, AiRagRetrievalLogger>();
            services.AddScoped<IAiRagLogger, AiRagLogger>();

            // ------------------------------------------------------------
            // Metrics
            // ------------------------------------------------------------
            services.AddSingleton<IAiRuntimeMetrics, AiRuntimeMetrics>();

            // ------------------------------------------------------------
            // Execution runtime
            // ------------------------------------------------------------
            services.AddScoped<AiSequentialExecutionEngine>();
            services.AddScoped<AiDagExecutionEngine>();

            // Legacy-compatible default engine registration.
            // Keep this if the default IAiExecutionEngine must remain sequential.
            services.AddScoped<IAiExecutionEngine, AiSequentialExecutionEngine>();

            // Step result normalization
            services.TryAddSingleton<IAiStepResultNormalizerPipeline, DefaultAiStepResultNormalizerPipeline>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IAiStepResultNormalizer, RagStepResultNormalizer>());

            return services;
        }
    }
}