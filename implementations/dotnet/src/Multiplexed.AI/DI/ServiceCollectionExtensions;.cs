using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Memory;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Metrics.Execution;
using Multiplexed.Abstractions.AI.Metrics.Policy;
using Multiplexed.Abstractions.AI.Metrics.Resolvers;
using Multiplexed.Abstractions.AI.Metrics.Retention;
using Multiplexed.Abstractions.AI.Metrics.Storage;
using Multiplexed.Abstractions.AI.Metrics.Workers;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Abstractions;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Providers;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry.Policies;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Engine.Batch;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Creation;
using Multiplexed.AI.Runtime.Execution.Engine.Distributed;
using Multiplexed.AI.Runtime.Execution.Engine.Finalization;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Execution.Engine.Local;
using Multiplexed.AI.Runtime.Execution.Engine.Retention;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;
using Multiplexed.AI.Runtime.Execution.Instance;
using Multiplexed.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Execution.Retention.Services;
using Multiplexed.AI.Runtime.Execution.Scheduling;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Memory;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Execution;
using Multiplexed.AI.Runtime.Metrics.HotState;
using Multiplexed.AI.Runtime.Metrics.Policy;
using Multiplexed.AI.Runtime.Metrics.Resolvers;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Multiplexed.AI.Runtime.Metrics.Storage;
using Multiplexed.AI.Runtime.Metrics.Workers;
using Multiplexed.AI.Runtime.Observability;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Steps.Execution;
using Multiplexed.AI.Runtime.Tracing;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.Realtime.Context;
using System.Reflection;

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
            // Execution retention trigger
            //
            // PURPOSE:
            // - Decides when the new retention service should run.
            // - Keeps retention trigger thresholds separate from retention policy options.
            // - Uses AiEngineOptions.RetentionTrigger as the configuration source.
            //
            // IMPORTANT:
            // - Trigger does not apply retention.
            // - Trigger does not mutate execution state.
            // - Trigger only decides whether retention evaluation should continue.
            // ------------------------------------------------------------


            services.TryAddScoped<IAiRetentionEngine>(_ => null!);
            services.TryAddScoped<IAiRetentionCompactionService, DefaultAiRetentionCompactionService>();
            services.TryAddScoped<IAiRetentionEvictionService, DefaultAiRetentionEvictionService>();

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
            // Tracing
            // ------------------------------------------------------------

            // services.AddSingleton<IAiRuntimeMetrics, AiRuntimeMetrics>();
            services.AddSingleton<IAiTraceTimeline, InMemoryAiTraceTimeline>();

            // Recorder 
            services.AddSingleton<IAiTraceRecorder>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AiEngineOptions>>().Value;
                var timeline = sp.GetRequiredService<IAiTraceTimeline>();

                return options.Observability.EnableInMemoryRecording
                    ? new InMemoryAiTraceRecorder(timeline)
                    : new NoOpAiTraceRecorder();
            });

            // Tracer 
            services.AddSingleton<IAiRuntimeTracer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AiEngineOptions>>().Value;
                var recorder = sp.GetRequiredService<IAiTraceRecorder>();

                return options.Observability.EnableTracing
                    ? new InMemoryAiRuntimeTracer(recorder)
                    : new NoOpAiRuntimeTracer();
            });

            // Facade observability
            services.AddScoped<IAiRuntimeObservability, AiRuntimeObservability>();

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
            // Metrics: execution, retention, storage, hot state, resolvers
            // ------------------------------------------------------------

            // Execution metrics
            services.TryAddSingleton<IAiExecutionMetrics, AiExecutionMetrics>();

            // Retention metrics
            services.TryAddSingleton<IAiRetentionTriggerMetrics, AiRetentionTriggerMetrics>();
            services.TryAddSingleton<IAiRetentionDecisionMetrics, AiRetentionDecisionMetrics>();
            services.TryAddSingleton<IAiRetentionPlanMetrics, AiRetentionPlanMetrics>();
            services.TryAddSingleton<IAiRetentionExecutionMetrics, AiRetentionExecutionMetrics>();
            services.TryAddSingleton<IAiRetentionMetrics, AiRetentionMetrics>();

            // Storage metrics
            services.TryAddSingleton<IAiStorageMetrics, AiStorageMetrics>();

            // Hot state metrics
            services.TryAddSingleton<IAiHotStateMetrics, AiHotStateMetrics>();

            // Resolver metrics
            services.TryAddSingleton<IAiResolverMetrics, AiResolverMetrics>();

            // Metrics facade
            services.TryAddSingleton<IAiRuntimeMetrics, AiRuntimeMetrics>();

            services.TryAddSingleton<IAiPolicyMetrics, AiPolicyMetrics>();

            services.TryAddSingleton<IAiRuntimeInstanceWorkerMetrics, AiRuntimeInstanceWorkerMetrics>();





            // ------------------------------------------------------------
            // Policy Registry
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiPolicyRegistry, DefaultAiPolicyRegistry>();

            services.AddAiPolicyEnginesFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly);

            services.AddAiPoliciesFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly);

            services.TryAddSingleton<IAiPolicyEngineFactory, DefaultAiPolicyEngineFactory>();


            // ------------------------------------------------------------
            // ExecutionOrchestrator Scheduler / Concurrency engine
            // ------------------------------------------------------------

            services.AddScoped<IAiDagStepExecutionOrchestrator,DefaultAiDagStepExecutionOrchestrator>();
            services.AddSingleton<IAiConcurrencyGate, RedisAiConcurrencyGate>();
            services.TryAddSingleton<IAiConcurrencyEngine, DefaultAiConcurrencyEngine>();

            // ------------------------------------------------------------
            // instance identity
            // ------------------------------------------------------------

            services.TryAddSingleton<IAiRuntimeInstanceIdentity, DefaultAiRuntimeInstanceIdentity>();
            services.TryAddScoped<IAiRuntimeInstanceWorker, AiRuntimeInstanceWorker>();

            services.TryAddSingleton<IOptions<AiRuntimeInstanceWorkerOptions>>(
                Options.Create(options.RuntimeInstanceWorker ?? new AiRuntimeInstanceWorkerOptions()));


            // ------------------------------------------------------------
            // global execution engine runtime services
            // ------------------------------------------------------------
            services.AddTransient<AiDagExecutionLifecycleHelper>();
            services.AddTransient<AiDagRetentionCoordinator>();
            services.AddTransient<AiDagStepClaimService>();
            services.AddTransient<AiDagClaimedStepExecutor>();
            services.AddTransient<AiDagExecutionFinalizationService>();
            services.AddTransient<AiDagExecutionCreator>();
            services.AddTransient<AiDagLocalExecutionRunner>();
            services.AddTransient<AiDagDistributedExecutionRunner>();
            services.AddTransient<AiDagBatchExecutionRunner>();

            services.AddTransient<IAiDagExecutionEngineRuntimeServices, AiDagExecutionEngineRuntimeServices>();

            // ------------------------------------------------------------
            // global execution engine services
            // ------------------------------------------------------------
            services.TryAddScoped<IAiDagExecutionEngineServices, AiDagExecutionEngineServices>();
            return services;
        }

        public static IServiceCollection AddAiPoliciesFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var policies = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IAiPolicy).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var policy in policies)
            {
                services.TryAddEnumerable(
                    ServiceDescriptor.Singleton(typeof(IAiPolicy), policy));
            }

            return services;
        }

        public static IServiceCollection AddAiPolicyEnginesFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var engineTypes = assemblies
                .SelectMany(x => x.GetTypes())
                .Where(x =>
                    !x.IsAbstract &&
                    !x.IsInterface &&
                    typeof(IAiPolicyEngine).IsAssignableFrom(x) &&
                    x.GetCustomAttributes(typeof(AiPolicyEngineAttribute), false).Any())
                .ToArray();

            services.TryAddSingleton<IAiPolicyEngineRegistry>(
                _ => new DefaultAiPolicyEngineRegistry(engineTypes));

            return services;
        }
    }
}