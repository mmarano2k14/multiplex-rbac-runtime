using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
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
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.AI.Tests.Integration.Models;
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

        [Fact]
        public async Task ExecuteNextAsync_Should_Execute_Root_First()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-parallel-basic", "Marco");

            var record = await engine.ExecuteNextAsync(created.ExecutionId);

            Assert.Contains("start", record.CompletedSteps);
        }

        [Fact]
        public async Task ExecuteNextAsync_Should_Complete_All_Steps_In_Order()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-parallel-basic", "Marco");

            var r1 = await engine.ExecuteNextAsync(created.ExecutionId);
            var r2 = await engine.ExecuteNextAsync(created.ExecutionId);
            var r3 = await engine.ExecuteNextAsync(created.ExecutionId);
            var r4 = await engine.ExecuteNextAsync(created.ExecutionId);

            Assert.Equal(AiExecutionStatus.Completed, r4.Status);

            Assert.Equal(4, r4.CompletedSteps.Count);
            Assert.Contains("merge", r4.CompletedSteps);
        }

        [Fact]
        public async Task ExecuteNextAsync_Should_Throw_When_Not_Dag_Mode()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var store = GetRequiredService<MemoryAiExecutionStore>(engine);

            var record = new AiExecutionRecord
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                PipelineName = "dag-parallel-basic",
                ExecutionMode = AiExecutionMode.Sequential,
                ContextKey = "ctx",
                Status = AiExecutionStatus.Pending
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId,
                PipelineName = record.PipelineName
            };

            await store.CreateAsync(record, state);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => engine.ExecuteNextAsync(record.ExecutionId));
        }

        // ================= ENGINE =================

        private static AiDagExecutionEngine CreateEngine()
        {
            var executionStore = new MemoryAiExecutionStore();


            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var contextStore = new MemoryContextStore(memoryCache, TimeSpan.FromMinutes(5));

            var accessor = new ExecutionContextAccessor();
            var contextFactory = new ExecutionContextFactory();

            var logger = new NoopLogger();
            var classifier = new DefaultAiRetryExceptionClassifier();
            var dataPolicy = new InlineAiExecutionDataPolicy();

            var stepExecutor = new AiStepExecutor(classifier, logger, dataPolicy);

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

            var aiOptions = new AiEngineOptions();

            aiOptions.Cleanup = new AiExecutionCleanupOptions
            {
                AutoCleanupOnCompleted = false,
                AutoCleanupOnFailed = false,
                SuppressCleanupExceptions = true
            };

            var metrics = new AiRuntimeMetrics();

            return new AiDagExecutionEngine(
                executionStore,
                contextStore,
                accessor,
                contextFactory,
                CreateServiceProvider(accessor, executionStore),
                pipelineExecutor,
                logger,cleanupService, Options.Create(aiOptions), metrics);
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

        private static IAiStepRegistry CreateStepRegistry()
        {
            var registry = new InMemoryAiStepRegistry();

            registry.Register(new TestStep("start"));
            registry.Register(new TestStep("a1"));
            registry.Register(new TestStep("a2"));
            registry.Register(new TestStep("merge"));

            return registry;
        }

        private static IServiceProvider CreateServiceProvider(
            ExecutionContextAccessor accessor,
            MemoryAiExecutionStore store)
        {
            return new TestServiceProvider(new Dictionary<Type, object>
            {
                [typeof(ExecutionContextAccessor)] = accessor,
                [typeof(MemoryAiExecutionStore)] = store
            });
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

        // ================= TEST STEPS =================

        private sealed class TestStep : IAiStep
        {
            private readonly string _name;

            public TestStep(string name)
            {
                _name = name;
            }

            public string Name => _name;

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AiStepResult.Ok($"executed {_name}"));
            }
        }

        private sealed class InMemoryAiStepRegistry : IAiStepRegistry
        {
            private readonly Dictionary<string, IAiStep> _steps = new();

            public void Register(IAiStep step)
            {
                _steps[step.Name] = step;
            }

            public IAiStep Resolve(string key)
            {
                return _steps[key];
            }
        }

        private sealed class TestServiceProvider : IServiceProvider
        {
            private readonly Dictionary<Type, object> _services;

            public TestServiceProvider(Dictionary<Type, object> services)
            {
                _services = services;
            }

            public object? GetService(Type serviceType)
            {
                return _services.TryGetValue(serviceType, out var s) ? s : null;
            }
        }
    }
}