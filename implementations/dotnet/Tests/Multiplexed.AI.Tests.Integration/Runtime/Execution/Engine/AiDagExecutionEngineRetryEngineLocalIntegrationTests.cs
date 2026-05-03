using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry.Policies;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.AI.Tests.Integration.Helpers;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Multiplexed.Rbac.Core.Stores.Memory;
using Xunit;
using static Multiplexed.AI.Tests.Integration.Helpers.MetricsFactory;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Engine
{
    /// <summary>
    /// Validates retry scheduling behavior for DAG execution.
    ///
    /// PURPOSE:
    /// - Validate existing distributed Redis/Lua retry behavior.
    /// - Validate local retry-engine behavior through <see cref="RetryExecutionAdapter"/>.
    /// - Validate local retry recovery from a transient failure to a successful second attempt.
    ///
    /// IMPORTANT:
    /// - Distributed mode uses <see cref="IAiDagExecutionStore"/> and legacy Lua retry state.
    /// - Local mode uses <see cref="IAiExecutionStore"/> and the new retry adapter.
    /// </summary>
    public sealed class AiDagRetryEngineLocalIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";

        [Fact]
        public async Task Distributed_Dag_Should_Schedule_Retry_When_Step_Fails()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddAiStepsFromAssemblies(
                        typeof(AiDagRetryEngineLocalIntegrationTests).Assembly);
                },
                ConnectionString,
                DatabaseName);

            var created = await host.Engine.CreateAsync(
                "dag-retry-fail",
                "hello");

            var record = await host.Engine.ExecuteNextAsync(
                created.ExecutionId);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var state = await dagStore.GetStateAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(state);

            var step = state!.Steps["step-1"];

            Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step.Status);
            Assert.Equal(1, step.RetryCount);
            Assert.NotNull(step.NextRetryAtUtc);

            Assert.True(
                record.Status == AiExecutionStatus.Running ||
                record.Status == AiExecutionStatus.Waiting,
                $"Unexpected execution status: {record.Status}");
        }

        [Fact]
        public async Task Local_Dag_Should_Schedule_Retry_With_Retry_Adapter_When_Step_Fails()
        {
            var engine = CreateLocalEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync(
                "dag-retry-local",
                "hello");

            var record = await engine.ExecuteNextAsync(
                created.ExecutionId);

            var store = GetRequiredService<MemoryAiExecutionStore>(engine);

            var state = await store.GetStateAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(state);

            var step = state!.Steps["step-1"];

            Assert.NotNull(step.Retry);
            Assert.NotNull(step.RetryState);

            Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step.Status);
            Assert.Equal(1, step.RetryState!.RetryCount);
            Assert.NotNull(step.RetryState.NextRetryAtUtc);

            Assert.True(
                record.Status == AiExecutionStatus.Running ||
                record.Status == AiExecutionStatus.Waiting,
                $"Unexpected execution status: {record.Status}");
        }

        [Fact]
        public async Task Local_Dag_Should_Retry_And_Succeed_On_Second_Attempt()
        {
            TestFailThenSuccessStep.Reset();

            var engine = CreateLocalEngine(CreateLocalRetrySuccessPipeline());

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync(
                "dag-retry-local-success",
                "hello");

            var store = GetRequiredService<MemoryAiExecutionStore>(engine);

            var firstRecord = await engine.ExecuteNextAsync(
                created.ExecutionId);

            var firstState = await store.GetStateAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(firstState);

            var firstStep = firstState!.Steps["step-1"];

            Assert.NotNull(firstStep.Retry);
            Assert.NotNull(firstStep.RetryState);
            Assert.Equal(AiStepExecutionStatus.WaitingForRetry, firstStep.Status);
            Assert.Equal(1, firstStep.RetryState!.RetryCount);
            Assert.NotNull(firstStep.RetryState.NextRetryAtUtc);

            Assert.True(
                firstRecord.Status == AiExecutionStatus.Running ||
                firstRecord.Status == AiExecutionStatus.Waiting,
                $"Unexpected execution status after first attempt: {firstRecord.Status}");

            var secondRecord = await engine.ExecuteNextAsync(
                created.ExecutionId);

            var secondState = await store.GetStateAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(secondState);

            var secondStep = secondState!.Steps["step-1"];

            Assert.Equal(AiStepExecutionStatus.Completed, secondStep.Status);
            Assert.NotNull(secondStep.RetryState);
            Assert.Equal(1, secondStep.RetryState!.RetryCount);
            Assert.Equal(AiExecutionStatus.Completed, secondRecord.Status);
        }

        private static AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\dag-retry-fail.json",

                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = false
                },

                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = false
                },

                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                }
            };
        }

        private static AiDagExecutionEngine CreateLocalEngine(AiPipelineDefinition? pipeline = null)
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
                typeof(AiDagRetryEngineLocalIntegrationTests).Assembly);

            var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<IAiStepRegistry>();
            var resolver = new AiPipelineResolver(registry);

            var sourceSelector = CreateInMemorySourceSelector(
                pipeline ?? CreateLocalRetryPipeline());

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

            var observability = ObservabilityFactory.Create();

            IAiExecutionStateWriter stateWriter = new DefaultAiExecutionStateWriter();

            IAiExecutionStateReader stateReader =
                new DefaultAiExecutionStateReader(new NoopPayloadResolver());

            var payloadCompactor = new NoopStepResultPayloadCompactor();
            var retentionService = new NoopExecutionRetentionService();
            var stepResolver = new LocalStateExecutionStepResolver();


            var retryPolicies = new IAiPolicy[]
{
                new DefaultTransientRetryPolicy(),
                new DefaultTimeoutRetryPolicy(),
                new DefaultRateLimitRetryPolicy()
            };

            var policyRegistry = new DefaultAiPolicyRegistry(retryPolicies);

            var policyEngineRegistry = new DefaultAiPolicyEngineRegistry(
                new[]
                {
                    typeof(DefaultAiRetryEngine)
                });

            var policyFactory = new DefaultAiPolicyEngineFactory(
                policyRegistry,
                policyEngineRegistry);

            var serviceProvider = new TestServiceProvider(new Dictionary<Type, object>
            {
                [typeof(ExecutionContextAccessor)] = accessor,
                [typeof(MemoryAiExecutionStore)] = executionStore,
                [typeof(IAiExecutionStore)] = executionStore,
                [typeof(IAiExecutionStateReader)] = stateReader,
                [typeof(IAiExecutionStateWriter)] = stateWriter,
                [typeof(IAiExecutionRetentionService)] = retentionService,
                [typeof(IAiExecutionStepResolver)] = stepResolver,
                [typeof(IAiPolicyEngineFactory)] = policyFactory
            });

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
                observability,
                payloadCompactor,
                stateReader,
                stateWriter,
                stepResolver,
                retentionService,
                policyFactory,
                null);

            return new AiDagExecutionEngine(engineServices);
        }

        private static AiPipelineDefinition CreateLocalRetryPipeline()
        {
            return new AiPipelineDefinition
            {
                Name = "dag-retry-local",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = new List<AiPipelineStepDefinition>
                {
                    new AiPipelineStepDefinition
                    {
                        Name = "step-1",
                        StepKey = "test.fail",
                        Order = 1,
                        DependsOn = new List<string>(),
                        Config = new Dictionary<string, object?>
                        {
                            ["retry"] = new Dictionary<string, object?>
                            {
                                ["policy"] = "retry.transient.default",
                                ["maxRetries"] = 3,
                                ["baseDelayMs"] = 50,
                                ["jitter"] = false
                            }
                        }
                    }
                }
            };
        }

        private static AiPipelineDefinition CreateLocalRetrySuccessPipeline()
        {
            return new AiPipelineDefinition
            {
                Name = "dag-retry-local-success",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = new List<AiPipelineStepDefinition>
                {
                    new AiPipelineStepDefinition
                    {
                        Name = "step-1",
                        StepKey = "test.fail.then.success",
                        Order = 1,
                        DependsOn = new List<string>(),
                        Config = new Dictionary<string, object?>
                        {
                            ["retry"] = new Dictionary<string, object?>
                            {
                                ["policy"] = "retry.transient.default",
                                ["maxRetries"] = 3,
                                ["baseDelayMs"] = 0,
                                ["jitter"] = false
                            }
                        }
                    }
                }
            };
        }

        private static IAiPipelineDefinitionSourceSelector CreateInMemorySourceSelector(
            AiPipelineDefinition pipeline)
        {
            var services = new ServiceCollection();

            services.AddOptions();

            services.Configure<AiEngineOptions>(options =>
            {
                options.DefaultPipelineDefinitionSource = "InMemory";
            });

            var provider = new InMemoryAiPipelineDefinitionProvider(
                new[] { pipeline });

            services.AddSingleton<IAiPipelineDefinitionProvider>(provider);
            services.AddSingleton(provider);

            var serviceProvider = services.BuildServiceProvider();

            return new DefaultAiPipelineDefinitionSourceSelector(
                serviceProvider.GetRequiredService<IOptions<AiEngineOptions>>(),
                serviceProvider);
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

        [AiStep("test.fail")]
        private sealed class TestFailStep : IAiStep
        {
            public string Name => "test.fail";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AiStepResult.Fail("boom"));
            }
        }

        [AiStep("test.fail.then.success")]
        private sealed class TestFailThenSuccessStep : IAiStep
        {
            private static int _counter;

            public string Name => "test.fail.then.success";

            public static void Reset()
            {
                _counter = 0;
            }

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                _counter++;

                if (_counter == 1)
                {
                    return Task.FromResult(AiStepResult.Fail("boom"));
                }

                return Task.FromResult(AiStepResult.Ok("ok"));
            }
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

        private sealed class NoopStepResultPayloadCompactor : IAiStepResultPayloadCompactor
        {
            public Task CompactAsync(
                AiStepResult result,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class NoopExecutionRetentionService : IAiExecutionRetentionService
        {
            public ValueTask<AiExecutionRetentionApplyResult> ApplyAsync(
                AiExecutionState state,
                AiExecutionRetentionMode mode,
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(AiExecutionRetentionApplyResult.Empty);
            }
        }

        private sealed class LocalStateExecutionStepResolver : IAiExecutionStepResolver
        {
            public Task WarmAsync(
                string executionId,
                AiExecutionState state,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task WarmStepsAsync(
                string executionId,
                AiExecutionState state,
                IReadOnlyCollection<string> stepNames,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<AiStepState?> GetStepAsync(
                string executionId,
                string stepName,
                AiExecutionState state,
                CancellationToken cancellationToken = default)
            {
                state.Steps.TryGetValue(stepName, out var step);
                return Task.FromResult(step);
            }

            public Task<AiStepState?> GetStepStatusAsync(
                string executionId,
                string stepName,
                AiExecutionState state,
                CancellationToken cancellationToken = default)
            {
                state.Steps.TryGetValue(stepName, out var step);
                return Task.FromResult(step);
            }
        }

        private sealed class TestServiceProvider : IServiceProvider
        {
            private readonly Dictionary<Type, object> _services;

            public TestServiceProvider(Dictionary<Type, object> services)
            {
                _services = services;
                _services[typeof(IServiceProvider)] = this;
            }

            public object? GetService(Type serviceType)
            {
                return _services.TryGetValue(serviceType, out var service)
                    ? service
                    : null;
            }
        }
    }
}
