using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.Metrics;
using Multiplexed.AI.Runtime.Execution.Normalization;
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
using Multiplexed.AI.Runtime.Retention.Triggers;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Multiplexed.Rbac.Core.Stores;
using Multiplexed.Rbac.Core.Stores.Cache;
using Multiplexed.Rbac.Core.Stores.Memory;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;
using static Multiplexed.AI.Tests.Integration.Runtime.Execution.AiDagExecutionEngineTests;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// End-to-end Redis integration tests for distributed retry behavior in <see cref="AiDagExecutionEngine"/>.
    ///
    /// COVERED BEHAVIORS:
    /// - first failure moves step into WaitingForRetry
    /// - retry delay prevents premature re-execution
    /// - retry promotion allows later re-execution
    /// - retry exhaustion converges to terminal failure
    /// - timeout recovery increments RecoveryCount without mutating business RetryCount
    ///
    /// ARCHITECTURE:
    /// - <see cref="AiExecutionState"/> is treated as a persistence model.
    /// - Step-state mutation/access is routed through <see cref="IAiExecutionStateWriter"/>.
    /// - Payload-aware reads are available through <see cref="IAiExecutionStateReader"/>.
    ///
    /// IMPORTANT:
    /// These tests never call CreateAsync(record, state) to patch an existing execution.
    /// The execution bundle is created once, then progressed only through engine/store operations.
    /// </summary>
    [Collection("redis")]
    public sealed class AiDagExecutionEngineRedisRetryIntegrationTests
    {
        private readonly IConnectionMultiplexer _connection;

        public AiDagExecutionEngineRedisRetryIntegrationTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            _connection = fixture.Connection;
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Move_Step_To_WaitingForRetry_On_FirstFailure()
        {
            var engine = CreateEngine("dag-retry-fail-once.json");

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-fail-once", "Marco");

            try
            {
                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);
                var state = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(state);

                var step = stateWriter.GetOrCreateStep(state!, "retry-step");

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step.Status);
                Assert.Equal(1, step.RetryCount);
                Assert.NotNull(step.NextRetryAtUtc);
                Assert.Null(step.ClaimedBy);
                Assert.Null(step.ClaimToken);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Not_Reexecute_Step_Before_RetryDelay()
        {
            var engine = CreateEngine("dag-retry-delay.json");

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-delay", "Marco");

            try
            {
                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                var second = await engine.ExecuteNextAsync(created.ExecutionId);

                Assert.True(
                    second.Status == AiExecutionStatus.Waiting ||
                    second.Status == AiExecutionStatus.Running);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);
                var state = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(state);

                var step = stateWriter.GetOrCreateStep(state!, "retry-step");

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step.Status);
                Assert.Equal(1, step.RetryCount);
                Assert.NotNull(step.NextRetryAtUtc);
                Assert.True(step.NextRetryAtUtc > DateTime.UtcNow);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Retry_And_Complete_When_RetryWindow_Is_Open()
        {
            var engine = CreateEngine("dag-retry-fail-once.json");

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-fail-once", "Marco");

            try
            {
                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);

                var beforeRetry = await dagStore.GetStateAsync(created.ExecutionId);
                Assert.NotNull(beforeRetry);

                var retryStep = stateWriter.GetOrCreateStep(beforeRetry!, "retry-step");

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, retryStep.Status);
                Assert.NotNull(retryStep.NextRetryAtUtc);

                await WaitForRetryWindowAsync(
                    dagStore,
                    stateWriter,
                    created.ExecutionId,
                    "retry-step");

                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                var finalState = await dagStore.GetStateAsync(created.ExecutionId);
                Assert.NotNull(finalState);

                var finalStep = stateWriter.GetOrCreateStep(finalState!, "retry-step");

                Assert.Equal(AiStepExecutionStatus.Completed, finalStep.Status);
                Assert.Equal(1, finalStep.RetryCount);
                Assert.Null(finalStep.NextRetryAtUtc);

                var finalRecord = await dagStore.GetRecordAsync(created.ExecutionId);
                Assert.NotNull(finalRecord);
                Assert.Equal(AiExecutionStatus.Completed, finalRecord!.Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task ExecuteAllAsync_Should_Fail_After_MaxRetries_Are_Exhausted()
        {
            var engine = CreateEngine("dag-retry-always-fail.json");

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-always-fail", "Marco");

            try
            {
                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);

                for (var i = 0; i < 10; i++)
                {
                    await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                    var state = await dagStore.GetStateAsync(created.ExecutionId);
                    Assert.NotNull(state);

                    var step = stateWriter.GetOrCreateStep(state!, "retry-step");

                    if (step.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        await WaitForRetryWindowAsync(
                            dagStore,
                            stateWriter,
                            created.ExecutionId,
                            "retry-step");
                    }

                    if (step.Status == AiStepExecutionStatus.Failed)
                    {
                        break;
                    }
                }

                var finalRecord = await dagStore.GetRecordAsync(created.ExecutionId);
                Assert.NotNull(finalRecord);
                Assert.Equal(AiExecutionStatus.Failed, finalRecord!.Status);

                var finalState = await dagStore.GetStateAsync(created.ExecutionId);
                Assert.NotNull(finalState);

                var finalStep = stateWriter.GetOrCreateStep(finalState!, "retry-step");

                Assert.Equal(AiStepExecutionStatus.Failed, finalStep.Status);
                Assert.Equal(finalStep.MaxRetries, finalStep.RetryCount);
                Assert.Null(finalStep.NextRetryAtUtc);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task RecoverTimedOutStepsAsync_Should_Increment_RecoveryCount_Without_Incrementing_RetryCount()
        {
            var dagStore = CreateDagStore();
            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "dag-recovery",
                ExecutionMode = AiExecutionMode.Dag,
                Status = AiExecutionStatus.Running,
                CompletedSteps = new List<string>()
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId
            };

            state.Steps["retry-step"] = new AiStepState
            {
                StepName = "retry-step",
                Status = AiStepExecutionStatus.Running,
                ClaimedBy = "worker-old",
                ClaimToken = "claim-old",
                ClaimedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-9),
                ClaimTimeoutSeconds = 30,
                RetryCount = 1,
                RecoveryCount = 0
            };

            await dagStore.CreateAsync(record, state);

            try
            {
                var recovered = await dagStore.RecoverTimedOutStepsAsync(executionId);

                Assert.Equal(1, recovered);

                var snapshot = await dagStore.GetStateAsync(executionId);

                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["retry-step"];

                Assert.Equal(AiStepExecutionStatus.Ready, step.Status);
                Assert.Null(step.ClaimedBy);
                Assert.Null(step.ClaimToken);
                Assert.Null(step.ClaimedAtUtc);
                Assert.Null(step.LeaseExpiresAtUtc);
                Assert.Equal(1, step.RetryCount);
                Assert.Equal(1, step.RecoveryCount);
            }
            finally
            {
                await CleanupDagExecutionAsync(executionId);
            }
        }

        private static async Task ExecuteIgnoringFailureAsync(
            AiDagExecutionEngine engine,
            string executionId)
        {
            try
            {
                await engine.ExecuteNextAsync(executionId);
            }
            catch
            {
                // Expected in tests where the step throws intentionally.
            }
        }

        private static async Task WaitForRetryWindowAsync(
            IAiDagExecutionStore dagStore,
            IAiExecutionStateWriter stateWriter,
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 50; i++)
            {
                var state = await dagStore.GetStateAsync(executionId, cancellationToken);
                if (state is null)
                {
                    throw new InvalidOperationException($"State '{executionId}' was not found.");
                }

                var step = stateWriter.GetOrCreateStep(state, stepName);

                if (!step.NextRetryAtUtc.HasValue)
                {
                    return;
                }

                var delay = step.NextRetryAtUtc.Value - DateTime.UtcNow;

                if (delay <= TimeSpan.Zero)
                {
                    return;
                }

                var wait = delay < TimeSpan.FromMilliseconds(25)
                    ? delay
                    : TimeSpan.FromMilliseconds(25);

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken);
                }
            }

            throw new TimeoutException(
                $"Retry window did not open in time for step '{stepName}' in execution '{executionId}'.");
        }

        private static void EnsureRetryPipelineFiles()
        {
            WritePipelineDefinitionToConfig(new AiPipelineDefinition
            {
                Name = "dag-retry-fail-once",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps =
                [
                    new AiPipelineStepDefinition
                    {
                        Name = "retry-step",
                        StepKey = "fail-once-then-succeed",
                        Order = 1,
                        DependsOn = [],
                        Execution = new AiPipelineStepExecutionDefinition
                        {
                            RetryDelayMs = 1000,
                            MaxRetries = 2
                        }
                    }
                ]
            });

            WritePipelineDefinitionToConfig(new AiPipelineDefinition
            {
                Name = "dag-retry-delay",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps =
                [
                    new AiPipelineStepDefinition
                    {
                        Name = "retry-step",
                        StepKey = "fail-once-then-succeed",
                        Order = 1,
                        DependsOn = [],
                        Execution = new AiPipelineStepExecutionDefinition
                        {
                            RetryDelayMs = 500,
                            MaxRetries = 1
                        }
                    }
                ]
            });

            WritePipelineDefinitionToConfig(new AiPipelineDefinition
            {
                Name = "dag-retry-always-fail",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps =
                [
                    new AiPipelineStepDefinition
                    {
                        Name = "retry-step",
                        StepKey = "always-fail",
                        Order = 1,
                        DependsOn = [],
                        Execution = new AiPipelineStepExecutionDefinition
                        {
                            RetryDelayMs = 1000,
                            MaxRetries = 3
                        }
                    }
                ]
            });
        }

        private AiDagExecutionEngine CreateEngine(string fileName)
        {
            EnsureRetryPipelineFiles();

            var executionStore = GetExecutionStore();
            var dagStore = CreateDagStore();

            var contextOptions = Options.Create(new ContextRuntimeOptions
            {
                UseRedisLuaScriptShaCaching = true
            });

            var redisContextStore = new RedisContextStore(_connection, contextOptions);
            var memoryContextStore = new MemoryContextStore(
                new MemoryCache(new MemoryCacheOptions()),
                TimeSpan.FromMinutes(5));

            var contextStore = new CompositeContextStore(redisContextStore, memoryContextStore);

            var accessor = new ExecutionContextAccessor();
            var contextFactory = new ExecutionContextFactory();
            var logger = new NoopLogger();
            var classifier = new DefaultAiRetryExceptionClassifier();

            var stepExecutor = new AiStepExecutor(classifier, logger);

            var services = new ServiceCollection();

            services.AddAiStepsFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly,
                typeof(AiDagExecutionEngineRedisRetryIntegrationTests).Assembly);

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<IAiStepRegistry>();
            var resolver = new AiPipelineResolver(registry);
            var sourceSelector = CreateJsonSourceSelector(fileName);

            var pipelineExecutor = new AiSequentialPipelineExecutor(
                sourceSelector,
                resolver,
                stepExecutor);

            var cleanupService = new NoOpAiExecutionCleanupService();
            var metrics = new AiRuntimeMetrics();

            var aiOptions = new AiEngineOptions
            {
                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                }
            };

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
                    CollectionName = $"payloads_retry_{Guid.NewGuid():N}"
                }
            });

            var payloadStore = new MongoAiPayloadStore(payloadOptions);
            var payloadStoreResolver = new FixedAiPayloadStoreResolver(payloadStore);

            var dataPolicy = new SmartInlineAiExecutionDataPolicy(
                payloadStoreResolver,
                payloadOptions);

            var metricsPayload = new InMemoryAiPayloadMetrics();
            var payloadCompactor = new DefaultAiStepResultPayloadCompactor(dataPolicy, metricsPayload);

            var stepPayloadStore = new DefaultAiStepPayloadStore(payloadStoreResolver);
            var stepPayloadIndexStore = new MongoAiStepPayloadIndexStore(payloadOptions);

            var stepResolver = new DefaultAiExecutionStepResolver(
                stepPayloadIndexStore,
                stepPayloadStore);

            var payloadResolver = new DefaultAiExecutionPayloadResolver(payloadStoreResolver);

            IAiExecutionStateWriter stateWriter = new DefaultAiExecutionStateWriter();
            IAiExecutionStateReader stateReader = new DefaultAiExecutionStateReader(payloadResolver);

            var retentionPolicy = CreateDisabledRetentionPolicy();

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
            var retentionTrigger = new DefaultAiExecutionRetentionTrigger(options.Value.RetentionTrigger);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(retentionTrigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var retentionService = Fixtures.AiDagExecutionEngineTestHost.CreateRetentionService(
                policyResolver,
                stepPayloadStore,
                stepPayloadIndexStore,
                payloadCompactor,
                retentionMetrics, decisionService);

            var engineServices = new AiDagExecutionEngineServices(
                executionStore,
                contextStore,
                accessor,
                contextFactory,
                CreateServiceProvider(
                    accessor,
                    executionStore,
                    dagStore,
                    retentionPolicy,
                    stateReader,
                    stateWriter,
                    stepResolver),
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
                dagStore);

            return new AiDagExecutionEngine(engineServices);
        }


        private IAiExecutionStore GetExecutionStore()
        {
            var keyBuilder = new AiExecutionKeyBuilder();
            var redis = new RedisAiExecutionStore(_connection, keyBuilder);
            var memory = new MemoryAiExecutionStore();
            return new AiExecutionStore(redis, memory);
        }

        private IAiDagExecutionStore CreateDagStore()
        {
            var logger = new NoopLogger();
            var keyBuilder = new AiExecutionKeyBuilder();
            var metrics = new AiRuntimeMetrics();
            var normalizers = new DefaultAiStepResultNormalizerPipeline([new RagStepResultNormalizer()]);
            return new RedisAiDagExecutionStore(_connection, keyBuilder, logger, metrics, normalizers);
        }

        private static IAiExecutionStateRetentionPolicy CreateDisabledRetentionPolicy()
        {
            return new DefaultAiExecutionStateRetentionPolicy(
                new AiExecutionStateRetentionOptions
                {
                    Enabled = false
                },
                new InMemoryAiExecutionRetentionMetrics());
        }

        private static IAiPipelineDefinitionSourceSelector CreateJsonSourceSelector(string fileName)
        {
            var services = new ServiceCollection();

            services.AddOptions();

            services.Configure<AiEngineOptions>(options =>
            {
                options.DefaultPipelineDefinitionSource = "Json";
            });

            services.AddSingleton<JsonAiPipelineDefinitionProvider>(_ =>
                new JsonAiPipelineDefinitionProvider("config/" + fileName));

            services.AddSingleton<InMemoryAiPipelineDefinitionProvider>();

            var provider = services.BuildServiceProvider();

            return new DefaultAiPipelineDefinitionSourceSelector(
                provider.GetRequiredService<IOptions<AiEngineOptions>>(),
                provider);
        }

        private static string WritePipelineDefinitionToConfig(AiPipelineDefinition definition)
        {
            var root = new
            {
                pipelines = new[] { definition }
            };

            var json = JsonSerializer.Serialize(
                root,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            var baseDir = AppContext.BaseDirectory;
            var configDir = Path.Combine(baseDir, "config");

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var filePath = Path.Combine(configDir, $"{definition.Name}.json");

            File.WriteAllText(filePath, json);

            return filePath;
        }

        private static IServiceProvider CreateServiceProvider(
    ExecutionContextAccessor accessor,
    IAiExecutionStore store,
    IAiDagExecutionStore dagStore,
    IAiExecutionStateRetentionPolicy retentionPolicy,
    IAiExecutionStateReader stateReader,
    IAiExecutionStateWriter stateWriter,
    IAiExecutionStepResolver stepResolver)
        {
            ArgumentNullException.ThrowIfNull(accessor);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(dagStore);
            ArgumentNullException.ThrowIfNull(retentionPolicy);
            ArgumentNullException.ThrowIfNull(stateReader);
            ArgumentNullException.ThrowIfNull(stateWriter);
            ArgumentNullException.ThrowIfNull(stepResolver);

            return new TestServiceProvider(new Dictionary<Type, object>
            {
                [typeof(ExecutionContextAccessor)] = accessor,
                [typeof(IAiExecutionStore)] = store,
                [typeof(IAiDagExecutionStore)] = dagStore,

                // Legacy retention policy (still used in tests / compatibility)
                [typeof(IAiExecutionStateRetentionPolicy)] = retentionPolicy,

                [typeof(IAiExecutionStateReader)] = stateReader,
                [typeof(IAiExecutionStateWriter)] = stateWriter,

                // required for selector + convergence with eviction
                [typeof(IAiExecutionStepResolver)] = stepResolver
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
            var property = typeof(AiExecutionEngine)
                .GetProperty("Services", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var provider = (IServiceProvider)property!.GetValue(engine)!;

            return (T)provider.GetService(typeof(T))!;
        }

        private async Task CleanupDagExecutionAsync(string executionId)
        {
            var db = _connection.GetDatabase();

            var recordKey = $"ai:execution:record:{executionId}";
            var stateKey = $"ai:execution:state:{executionId}";
            var stepsIndexKey = $"ai:execution:steps:{executionId}";

            var stepNames = await db.SetMembersAsync(stepsIndexKey);

            foreach (var stepName in stepNames)
            {
                if (stepName.IsNullOrEmpty)
                    continue;

                await db.KeyDeleteAsync($"ai:execution:step:{executionId}:{stepName}");
            }

            await db.KeyDeleteAsync(recordKey);
            await db.KeyDeleteAsync(stateKey);
            await db.KeyDeleteAsync(stepsIndexKey);
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
                return _services.TryGetValue(serviceType, out var service)
                    ? service
                    : null;
            }
        }
    }
}