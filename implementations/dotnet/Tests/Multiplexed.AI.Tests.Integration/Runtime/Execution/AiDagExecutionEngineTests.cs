using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;
using Multiplexed.Abstractions.AI.Execution.Retention.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Multiplexed.AI.Runtime.Retention;
using Multiplexed.AI.Runtime.Retention.Decisions;
using Multiplexed.AI.Runtime.Retention.Policies;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Multiplexed.Rbac.Core.Stores.Memory;
using Xunit;
using static Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures.AiDagExecutionEngineTestHost;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    public sealed class AiDagExecutionEngineTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_Basic_Dag_Pipeline()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-parallel-basic", "Marco");

            var finalRecord = await engine.ExecuteAllAsync(created.ExecutionId);

            Assert.NotNull(finalRecord);
            Assert.Equal(AiExecutionMode.Dag, finalRecord.ExecutionMode);
            Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);

            Assert.Equal(4, finalRecord.CompletedSteps.Count);
            Assert.Contains("start", finalRecord.CompletedSteps);
            Assert.Contains("a1", finalRecord.CompletedSteps);
            Assert.Contains("a2", finalRecord.CompletedSteps);
            Assert.Contains("merge", finalRecord.CompletedSteps);

            var store = GetRequiredService<MemoryAiExecutionStore>(engine);
            var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

            var state = await store.GetStateAsync(finalRecord.ExecutionId);

            Assert.NotNull(state);

            Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state!, "start").Status);
            Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state, "a1").Status);
            Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state, "a2").Status);
            Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state, "merge").Status);
        }

        private static AiDagExecutionEngine CreateEngine()
        {
            var executionStore = new MemoryAiExecutionStore();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var contextStore = new MemoryContextStore(memoryCache, TimeSpan.FromMinutes(5));

            var accessor = new ExecutionContextAccessor();
            var contextFactory = new ExecutionContextFactory();

            var logger = new NoopLogger();
            var classifier = new DefaultAiRetryExceptionClassifier();

            var stepExecutor = new AiStepExecutor(classifier, logger);

            var services = new ServiceCollection();

            services.AddAiStepsFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly,
                typeof(AiDagExecutionEngineTests).Assembly);

            var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<IAiStepRegistry>();
            var resolver = new AiPipelineResolver(registry);

            var sourceSelector = CreateJsonSourceSelector();

            var pipelineExecutor = new AiSequentialPipelineExecutor(
                sourceSelector,
                resolver,
                stepExecutor);

            var cleanupService = new NoOpAiExecutionCleanupService();

            var aiOptions = new AiEngineOptions
            {
                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                },
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = false,
                    Mode = AiExecutionRetentionMode.None
                }
            };

            var metrics = new AiRuntimeMetrics();

            var payloadOptions = Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = "mongo",
                RequireReplaySafePayloads = true,
                MaxInlineSizeBytes = 2048,
                Mongo = new MongoAiPayloadStoreOptions
                {
                    Enabled = true,
                    ConnectionString = "mongodb://localhost:27017",
                    DatabaseName = "multiplexed_ai_tests",
                    CollectionName = $"payloads_basic_dag_{Guid.NewGuid():N}"
                }
            });

            var payloadStore = new MongoAiPayloadStore(payloadOptions);
            var payloadStoreResolver = new FixedAiPayloadStoreResolver(payloadStore);

            var dataPolicy = new SmartInlineAiExecutionDataPolicy(
                payloadStoreResolver,
                payloadOptions);

            var metricsPayload = new InMemoryAiPayloadMetrics();

            var payloadCompactor = new DefaultAiStepResultPayloadCompactor(
                dataPolicy,
                metricsPayload);

            var stepPayloadStore = new DefaultAiStepPayloadStore(payloadStoreResolver);
            var stepPayloadIndexStore = new MongoAiStepPayloadIndexStore(payloadOptions);

            var stepResolver = new DefaultAiExecutionStepResolver(
                stepPayloadIndexStore,
                stepPayloadStore);

            var retentionPolicy = new DefaultAiExecutionStateRetentionPolicy(
                aiOptions.StateRetention,
                new InMemoryAiExecutionRetentionMetrics());

            IAiExecutionStateWriter stateWriter = new DefaultAiExecutionStateWriter();

            IAiExecutionStateReader stateReader =
                new DefaultAiExecutionStateReader(new NoopPayloadResolver());

            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    MaxCompletedStepsInState = 20,
                    Mode = AiExecutionRetentionMode.Evict
                }
            });

            var policies = new IAiExecutionRetentionPolicy[]
            {
            new NoopAiExecutionRetentionPolicy(),
            new CompactAiExecutionRetentionPolicy(),
            new EvictAiExecutionRetentionPolicy(options),
            new HybridAiExecutionRetentionPolicy(options)
            };

            var policyResolver = new DefaultAiExecutionRetentionPolicyResolver(policies);
            var retentionMetrics = new InMemoryAiExecutionRetentionServiceMetrics();

            var retentionService = CreateRetentionService(
                policyResolver,
                stepPayloadStore,
                stepPayloadIndexStore,
                payloadCompactor,
                retentionMetrics);

            var serviceProvider = CreateServiceProvider(
                accessor,
                executionStore,
                retentionPolicy,
                stateReader,
                stateWriter,
                retentionService,
                stepResolver);

            var engineServices = new AiDagExecutionEngineServices(
                executionStore,
                contextStore,
                accessor,
                contextFactory,
                serviceProvider,
                pipelineExecutor,
                logger,
                cleanupService,
                Options.Create(aiOptions),
                metrics,
                payloadCompactor,
                stateReader,
                stateWriter,
                stepResolver,
                retentionService,
                null);

            return new AiDagExecutionEngine(engineServices);
        }

        private static IAiExecutionRetentionService CreateRetentionService(
            IAiExecutionRetentionPolicyResolver policyResolver,
            IAiStepPayloadStore stepPayloadStore,
            IAiStepPayloadIndexStore stepPayloadIndexStore,
            IAiStepResultPayloadCompactor payloadCompactor,
            IAiExecutionRetentionServiceMetrics metrics)
        {
            ArgumentNullException.ThrowIfNull(policyResolver);
            ArgumentNullException.ThrowIfNull(stepPayloadStore);
            ArgumentNullException.ThrowIfNull(stepPayloadIndexStore);
            ArgumentNullException.ThrowIfNull(payloadCompactor);
            ArgumentNullException.ThrowIfNull(metrics);

            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            return new AiExecutionRetentionService(
                policyResolver,
                stepPayloadStore,
                stepPayloadIndexStore,
                payloadCompactor,
                metrics, decisionService);
        }

        private static IServiceProvider CreateServiceProvider(
            ExecutionContextAccessor accessor,
            MemoryAiExecutionStore store,
            IAiExecutionStateRetentionPolicy retentionPolicy,
            IAiExecutionStateReader stateReader,
            IAiExecutionStateWriter stateWriter,
            IAiExecutionRetentionService retentionService,
            IAiExecutionStepResolver stepResolver)
        {
            return new TestServiceProvider(new Dictionary<Type, object>
            {
                [typeof(ExecutionContextAccessor)] = accessor,
                [typeof(MemoryAiExecutionStore)] = store,
                [typeof(IAiExecutionStore)] = store,
                [typeof(IAiExecutionStateRetentionPolicy)] = retentionPolicy,
                [typeof(IAiExecutionStateReader)] = stateReader,
                [typeof(IAiExecutionStateWriter)] = stateWriter,
                [typeof(IAiExecutionRetentionService)] = retentionService,
                [typeof(IAiExecutionStepResolver)] = stepResolver
            });
        }

        private static IAiPipelineDefinitionSourceSelector CreateJsonSourceSelector()
        {
            var services = new ServiceCollection();

            services.AddOptions();

            services.Configure<AiEngineOptions>(options =>
            {
                options.DefaultPipelineDefinitionSource = "Json";
            });

            services.AddSingleton<JsonAiPipelineDefinitionProvider>(_ =>
                new JsonAiPipelineDefinitionProvider("config/dag-parallel-basic.json"));

            services.AddSingleton<InMemoryAiPipelineDefinitionProvider>();

            var provider = services.BuildServiceProvider();

            return new DefaultAiPipelineDefinitionSourceSelector(
                provider.GetRequiredService<IOptions<AiEngineOptions>>(),
                provider);
        }

        private static ExecutionContext CreateRuntimeContext()
        {
            return new ExecutionContext
            {
                ContextKey = string.Empty,
                Project = "Project",
                TenantId = "tenant-id-xxxx",
                TenantGroupId = "tenant-group-id-xxx",
                CurrentNamespace = "Namespace",
                UserId = "userId",
                Namespaces = new List<NamespaceEntry>
                {
                    new NamespaceEntry
                    {
                        Name = "Namespace",
                        Trns = new HashSet<string>
                        {
                            "trn:Project:crm:billing:invoice:read",
                            "trn:Project:crm:billing:invoice:refund"
                        }
                    }
                },
                TtlSeconds = 300
            };
        }

        private static T GetRequiredService<T>(AiDagExecutionEngine engine)
        {
            var property = typeof(AiExecutionEngine)
                .GetProperty(
                    "Services",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

            var provider = (IServiceProvider)property!.GetValue(engine)!;

            return (T)(provider.GetService(typeof(T))
                ?? throw new InvalidOperationException(
                    $"Required service '{typeof(T).FullName}' is not registered."));
        }

        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this test.");
            }
        }

        private sealed class TestServiceProvider : IServiceProvider
        {
            private readonly Dictionary<Type, object> _services;

            public TestServiceProvider(Dictionary<Type, object> services)
            {
                _services = services;
            }

            public void RegisterSelf()
            {
                _services[typeof(IServiceProvider)] = this;
            }

            public object? GetService(Type serviceType)
            {
                return _services.TryGetValue(serviceType, out var service)
                    ? service
                    : null;
            }
        }

        internal sealed class FixedAiPayloadStoreResolver : IAiPayloadStoreResolver
        {
            private readonly IAiPayloadStore _store;

            public FixedAiPayloadStoreResolver(IAiPayloadStore store)
            {
                _store = store;
            }

            public IAiPayloadStore Resolve()
            {
                return _store;
            }
        }
    }
}
