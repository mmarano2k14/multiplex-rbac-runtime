using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Retention;
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
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Multiplexed.Rbac.Core.Stores.Memory;
using Xunit;
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
            var state = await store.GetStateAsync(finalRecord.ExecutionId);

            Assert.NotNull(state);

            Assert.Equal(AiStepExecutionStatus.Completed, state!.GetOrCreateStep("start").Status);
            Assert.Equal(AiStepExecutionStatus.Completed, state.GetOrCreateStep("a1").Status);
            Assert.Equal(AiStepExecutionStatus.Completed, state.GetOrCreateStep("a2").Status);
            Assert.Equal(AiStepExecutionStatus.Completed, state.GetOrCreateStep("merge").Status);
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
                typeof(AiDagExecutionEngineTests).Assembly
            );

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
                }
            };

            var metrics = new AiRuntimeMetrics();

            var payloadStore = new InMemoryAiPayloadStore();
            var payloadStoreResolver = new FixedAiPayloadStoreResolver(payloadStore);

            var payloadOptions = Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = "inmemory",
                RequireReplaySafePayloads = false,
                MaxInlineSizeBytes = 2048
            });

            var dataPolicy = new SmartInlineAiExecutionDataPolicy(
                payloadStoreResolver,
                payloadOptions);

            var metricsPayload = new InMemoryAiPayloadMetrics();

            var payloadCompactor = new DefaultAiStepResultPayloadCompactor(
                dataPolicy,
                metricsPayload);

            var retentionPolicy = new DefaultAiExecutionStateRetentionPolicy(
                new AiExecutionStateRetentionOptions
                {
                    Enabled = false
                },
                new InMemoryAiExecutionRetentionMetrics());

            return new AiDagExecutionEngine(
                executionStore,
                contextStore,
                accessor,
                contextFactory,
                CreateServiceProvider(accessor, executionStore, retentionPolicy),
                pipelineExecutor,
                logger,
                cleanupService,
                Options.Create(aiOptions),
                metrics,
                payloadCompactor);
        }

        private static IServiceProvider CreateServiceProvider(
            ExecutionContextAccessor accessor,
            MemoryAiExecutionStore store,
            IAiExecutionStateRetentionPolicy retentionPolicy)
        {
            var provider = new TestServiceProvider(new Dictionary<Type, object>
            {
                [typeof(ExecutionContextAccessor)] = accessor,
                [typeof(MemoryAiExecutionStore)] = store,
                [typeof(IAiExecutionStateRetentionPolicy)] = retentionPolicy
            });

            // 🔥 FIX CRITIQUE
            provider.RegisterSelf();

            return provider;
        }

        private static IAiPipelineDefinitionSourceSelector CreateJsonSourceSelector()
        {
            var services = new ServiceCollection();

            services.AddOptions();

            services.Configure<AiEngineOptions>(options =>
            {
                options.DefaultPipelineDefinitionSource = "Json";
            });

            services.AddSingleton<JsonAiPipelineDefinitionProvider>(sp =>
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
            var field = typeof(AiExecutionEngine)
                .GetProperty("Services", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var provider = (IServiceProvider)field!.GetValue(engine)!;

            return (T)provider.GetService(typeof(T))!;
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
                return _services.TryGetValue(serviceType, out var s) ? s : null;
            }
        }

        internal sealed class FixedAiPayloadStoreResolver : IAiPayloadStoreResolver
        {
            private readonly IAiPayloadStore _store;

            public FixedAiPayloadStoreResolver(IAiPayloadStore store)
            {
                _store = store;
            }

            public IAiPayloadStore Resolve() => _store;
        }
    }
}