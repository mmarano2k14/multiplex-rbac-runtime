using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.State;
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
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Memory;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Multiplexed.AI.Runtime.Retention;
using Multiplexed.AI.Runtime.Retention.Policies;
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
        public static IServiceCollection AddMultiplexAI(
            this IServiceCollection services,
            AiEngineOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            // ------------------------------------------------------------
            // Options
            // ------------------------------------------------------------

            services.AddSingleton<IOptions<AiEngineOptions>>(Options.Create(options));
            services.AddSingleton<IOptions<AiExecutionCleanupOptions>>(
                Options.Create(options.Cleanup ?? new AiExecutionCleanupOptions()));

            // ------------------------------------------------------------
            // State readers and writers
            //
            // IMPORTANT:
            // - These services only manipulate hot execution state.
            // - They must not become payload/index/store abstractions.
            // - Retention remains external to this reader/writer layer.
            // ------------------------------------------------------------

            services.TryAddScoped<IAiExecutionStateReader, DefaultAiExecutionStateReader>();
            services.TryAddScoped<IAiExecutionStateWriter, DefaultAiExecutionStateWriter>();

            // ------------------------------------------------------------
            // Legacy execution state retention policy
            //
            // IMPORTANT:
            // - Kept for backward compatibility with existing constructors/tests.
            // - This is the old state-level retention policy.
            // - The new retention system below is policy/resolver/service based.
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiExecutionStateRetentionPolicy>(sp =>
            {
                var resolvedOptions = sp.GetRequiredService<IOptions<AiEngineOptions>>().Value;
                return new DefaultAiExecutionStateRetentionPolicy(resolvedOptions.StateRetention);
            });

            // ------------------------------------------------------------
            // New execution retention system
            //
            // DESIGN:
            // - IAiExecutionRetentionPolicy = pure decision.
            // - IAiExecutionRetentionPolicyResolver = selects policy by mode.
            // - IAiExecutionRetentionService = applies compaction / eviction safely.
            //
            // IMPORTANT:
            // - Policies must not mutate state.
            // - Policies must not write payloads.
            // - The service applies the plan in the safe order:
            //   1. compact result payloads
            //   2. persist full step payloads
            //   3. evict from hot state
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiExecutionRetentionPolicy, NoopAiExecutionRetentionPolicy>();
            services.TryAddSingleton<IAiExecutionRetentionPolicy, CompactAiExecutionRetentionPolicy>();
            services.TryAddSingleton<IAiExecutionRetentionPolicy, EvictAiExecutionRetentionPolicy>();
            services.TryAddSingleton<IAiExecutionRetentionPolicy, HybridAiExecutionRetentionPolicy>();

            services.TryAddSingleton<IAiExecutionRetentionPolicyResolver, DefaultAiExecutionRetentionPolicyResolver>();

            // ------------------------------------------------------------
            // Retention metrics (NEW SYSTEM)
            //
            // PURPOSE:
            // - Tracks Compact / Evict / Hybrid activity.
            // - Allows integration tests to validate the new retention system.
            // - Provides runtime observability for state reduction.
            // ------------------------------------------------------------

            services.TryAddSingleton<
                IAiExecutionRetentionServiceMetrics,
                InMemoryAiExecutionRetentionServiceMetrics>();

            // ------------------------------------------------------------
            // Retention service (NEW SYSTEM)
            //
            // IMPORTANT:
            // - Depends on policy resolver, step payload store, compactor, and metrics.
            // - Applies retention in the safe order:
            //   1. compact
            //   2. persist step payload
            //   3. evict from hot state
            // ------------------------------------------------------------

            services.TryAddSingleton<
                IAiExecutionRetentionService,
                AiExecutionRetentionService>();

            // ------------------------------------------------------------
            // Payload store: options
            // ------------------------------------------------------------

            services.Configure<AiPayloadStoreOptions>(opts =>
            {
                opts.Enabled = options.PayloadStore.Enabled;
                opts.Provider = options.PayloadStore.Provider;
                opts.RequireReplaySafePayloads = options.PayloadStore.RequireReplaySafePayloads;
                opts.MaxInlineSizeBytes = options.PayloadStore.MaxInlineSizeBytes;

                opts.Mongo = options.PayloadStore.Mongo;
                opts.RedisCache = options.PayloadStore.RedisCache;
                opts.StepIndexCache = options.PayloadStore.StepIndexCache;
            });

            // ------------------------------------------------------------
            // Payload store: concrete stores
            //
            // IMPORTANT:
            // - Do not expose IAiPayloadStore directly here.
            // - IAiPayloadStore must be resolved only through IAiPayloadStoreResolver.
            // - This keeps provider selection centralized.
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiPayloadMetrics, InMemoryAiPayloadMetrics>();
            services.TryAddSingleton<InMemoryAiPayloadStore>();
            services.TryAddSingleton<MongoAiPayloadStore>();
            services.TryAddSingleton<RedisAiPayloadStore>();
            services.TryAddSingleton<MongoRedisCachedAiPayloadStore>();

            // ------------------------------------------------------------
            // Step payload index store
            //
            // PURPOSE:
            // - Keeps AiExecutionState.Steps as hot state only.
            // - Preserves knowledge of evicted / archived steps outside the state.
            // - Allows selector, convergence, replay, and diagnostics to resolve
            //   steps after retention eviction.
            //
            // DESIGN:
            // - MongoAiStepPayloadIndexStore is the durable source of truth.
            // - RedisAiStepPayloadIndexCache is an optional fast mirror with TTL.
            // - RedisCachedAiStepPayloadIndex decorates Mongo with Redis cache.
            //
            // IMPORTANT:
            // - The index is not the payload store.
            // - The full serialized step state is stored by IAiStepPayloadStore.
            // - The index stores only the external payload reference.
            // ------------------------------------------------------------

            // Durable store (Mongo)
            services.TryAddSingleton<MongoAiStepPayloadIndexStore>();

            // Redis cache implementation
            services.TryAddSingleton<
                IAiStepPayloadIndexCache,
                RedisCachedAiStepPayloadIndex>();

            // Decorator store (Mongo + Redis)
            services.TryAddSingleton<CachedAiStepPayloadIndexStore>();

            // Resolver binding
            services.TryAddSingleton<IAiStepPayloadIndexStore>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AiPayloadStoreOptions>>().Value;

                if (options.StepIndexCache.Enabled &&
                    options.RedisCache.Enabled)
                {
                    return sp.GetRequiredService<CachedAiStepPayloadIndexStore>();
                }

                return sp.GetRequiredService<MongoAiStepPayloadIndexStore>();
            });

            // ------------------------------------------------------------
            // Step resolver
            //
            // PURPOSE:
            // - Resolves steps from hot state first.
            // - Falls back to the archived step index and step payload store.
            // - Keeps DAG selector and convergence correct after eviction.
            // ------------------------------------------------------------

            //services.TryAddSingleton<IAiExecutionStepResolver, DefaultAiExecutionStepResolver>();
            services.TryAddScoped<IAiExecutionStepResolver, DefaultAiExecutionStepResolver>();

            // ------------------------------------------------------------
            // Payload compaction
            //
            // PURPOSE:
            // - Externalizes heavy AiStepResult payloads.
            // - Used by compact/hybrid retention modes.
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiStepResultPayloadCompactor, DefaultAiStepResultPayloadCompactor>();

            // ------------------------------------------------------------
            // Step payload store
            //
            // PURPOSE:
            // - Step-aware wrapper used by retention eviction.
            // - Saves and loads complete AiStepState objects externally.
            //
            // IMPORTANT:
            // - IAiStepPayloadStore uses IAiPayloadStoreResolver internally.
            // - It does not replace the generic payload store.
            // - It does not mutate AiExecutionState.
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiStepPayloadStore, DefaultAiStepPayloadStore>();

            // ------------------------------------------------------------
            // Payload resolver
            //
            // Resolver = unique entry point for payload provider selection.
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiPayloadStoreResolver, DefaultAiPayloadStoreResolver>();
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
            // Context helpers
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiContextValueResolver, DefaultAiContextValueResolver>();
            services.TryAddSingleton<IAiStepContextHelperFactory, DefaultAiStepContextHelperFactory>();

            // ------------------------------------------------------------
            // Execution runtime
            // ------------------------------------------------------------

            services.AddScoped<AiSequentialExecutionEngine>();
            services.AddScoped<AiDagExecutionEngine>();

            // Legacy-compatible default engine registration.
            // Keep this if the default IAiExecutionEngine must remain sequential.
            services.AddScoped<IAiExecutionEngine, AiSequentialExecutionEngine>();

            // ------------------------------------------------------------
            // Step result normalization
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiStepResultNormalizerPipeline, DefaultAiStepResultNormalizerPipeline>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IAiStepResultNormalizer, RagStepResultNormalizer>());


            // ------------------------------------------------------------
            // global execution engine services
            // ------------------------------------------------------------
            services.TryAddScoped<IAiDagExecutionEngineServices, AiDagExecutionEngineServices>();

            return services;
        }
    }
}