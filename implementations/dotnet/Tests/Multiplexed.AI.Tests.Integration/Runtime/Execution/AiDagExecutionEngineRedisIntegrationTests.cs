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
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.AI.Retry;
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
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.AI.Tests.Fixtures;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Helpers;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.AI.Tests.Integration.Models;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Multiplexed.Rbac.Core.Stores;
using Multiplexed.Rbac.Core.Stores.Cache;
using Multiplexed.Rbac.Core.Stores.Memory;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;
using static Multiplexed.AI.Tests.Integration.Helpers.MetricsFactory;
using static Multiplexed.AI.Tests.Integration.Runtime.Execution.AiDagExecutionEngineTests;
using static Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures.AiDagExecutionEngineTestHost;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// End-to-end Redis integration tests for <see cref="AiDagExecutionEngine"/>
    /// using the distributed DAG Redis/Lua store.
    ///
    /// PURPOSE:
    /// - Validate DAG execution creation.
    /// - Validate step-by-step distributed progression.
    /// - Validate ExecuteAll orchestration.
    /// - Validate persisted step-state propagation across Redis round-trips.
    /// - Validate claim ownership safety.
    /// - Validate timeout recovery.
    ///
    /// ARCHITECTURE:
    /// - <see cref="AiExecutionState"/> is treated as a persistence model.
    /// - Step state mutation is routed through <see cref="IAiExecutionStateWriter"/>.
    /// - State reading is routed through <see cref="IAiExecutionStateReader"/>.
    /// </summary>
    [Collection("redis")]
    public sealed class AiDagExecutionEngineRedisIntegrationTests
    {
        private readonly IConnectionMultiplexer _connection;

        public AiDagExecutionEngineRedisIntegrationTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);

            _connection = fixture.Connection;
        }

        [RedisFact]
        public async Task ExecuteAllAsync_Should_Complete_Basic_Dag_Pipeline()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                var finalRecord = await engine.ExecuteAllAsync(created.ExecutionId);

                Assert.NotNull(finalRecord);
                Assert.Equal(AiExecutionMode.Dag, finalRecord.ExecutionMode);
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);

                Assert.Equal(4, finalRecord.CompletedSteps.Count);
                Assert.Contains("start", finalRecord.CompletedSteps);
                Assert.Contains("a1", finalRecord.CompletedSteps);
                Assert.Contains("a2", finalRecord.CompletedSteps);
                Assert.Contains("merge", finalRecord.CompletedSteps);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);
                var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

                var state = await dagStore.GetStateAsync(finalRecord.ExecutionId);

                Assert.NotNull(state);
                Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state!, "start").Status);
                Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state, "a1").Status);
                Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state, "a2").Status);
                Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state, "merge").Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Execute_Root_First()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                var record = await engine.ExecuteNextAsync(created.ExecutionId);

                Assert.Contains("start", record.CompletedSteps);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);
                var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

                var state = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(state);
                Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(state!, "start").Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Complete_All_Steps_In_Order()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                _ = await engine.ExecuteNextAsync(created.ExecutionId);
                _ = await engine.ExecuteNextAsync(created.ExecutionId);
                _ = await engine.ExecuteNextAsync(created.ExecutionId);
                var r4 = await engine.ExecuteNextAsync(created.ExecutionId);

                Assert.Equal(AiExecutionStatus.Completed, r4.Status);
                Assert.Equal(4, r4.CompletedSteps.Count);
                Assert.Contains("merge", r4.CompletedSteps);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Persist_Dag_State_Across_Redis_Reload()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                var afterFirst = await engine.ExecuteNextAsync(created.ExecutionId);

                Assert.Contains("start", afterFirst.CompletedSteps);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);
                var stateWriter = GetRequiredService<IAiExecutionStateWriter>(engine);

                var persistedRecord = await dagStore.GetRecordAsync(created.ExecutionId);
                var persistedState = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(persistedRecord);
                Assert.NotNull(persistedState);

                Assert.Equal(AiExecutionMode.Dag, persistedRecord!.ExecutionMode);
                Assert.Contains("start", afterFirst.CompletedSteps);
                Assert.Equal(AiStepExecutionStatus.Completed, stateWriter.GetOrCreateStep(persistedState!, "start").Status);

                var finalRecord = await engine.ExecuteAllAsync(created.ExecutionId);

                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
                Assert.Equal(4, finalRecord.CompletedSteps.Count);
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Throw_When_Not_Dag_Mode()
        {
            var engine = CreateEngine();

            var accessor = GetRequiredService<ExecutionContextAccessor>(engine);
            accessor.Set(CreateRuntimeContext());

            var store = GetRequiredService<IAiExecutionStore>(engine);

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

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => engine.ExecuteNextAsync(record.ExecutionId));
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
            }
        }

        [RedisFact]
        public async Task TryClaimNextReadyStepAsync_Should_Allow_Only_One_Worker_To_Claim_Root_Step()
        {
            var dagStore = CreateDagStore();
            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "dag",
                ExecutionMode = AiExecutionMode.Dag,
                Status = AiExecutionStatus.Running
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId
            };

            state.Steps["start"] = new AiStepState
            {
                StepName = "start",
                Status = AiStepExecutionStatus.Ready
            };

            await dagStore.CreateAsync(record, state);

            try
            {
                var claims = await Task.WhenAll(
                    dagStore.TryClaimNextReadyStepAsync(executionId, "worker-1"),
                    dagStore.TryClaimNextReadyStepAsync(executionId, "worker-2"),
                    dagStore.TryClaimNextReadyStepAsync(executionId, "worker-3"));

                var success = claims.Where(x => x is not null).ToArray();

                Assert.Single(success);
                Assert.Equal("start", success[0]!.StepName);
            }
            finally
            {
                await CleanupDagExecutionAsync(executionId);
            }
        }

        [RedisFact]
        public async Task TryClaimNextReadyStepAsync_Should_Not_Claim_Dependent_Step_Before_Dependency_Is_Completed()
        {
            var dagStore = CreateDagStore();
            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "dag",
                ExecutionMode = AiExecutionMode.Dag,
                Status = AiExecutionStatus.Running
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId
            };

            state.Steps["start"] = new AiStepState
            {
                StepName = "start",
                Status = AiStepExecutionStatus.Ready
            };

            state.Steps["merge"] = new AiStepState
            {
                StepName = "merge",
                Status = AiStepExecutionStatus.Ready,
                DependsOn = new List<string> { "start" }
            };

            await dagStore.CreateAsync(record, state);

            try
            {
                var claim1 = await dagStore.TryClaimNextReadyStepAsync(executionId, "worker-1");

                Assert.NotNull(claim1);
                Assert.Equal("start", claim1!.StepName);

                var claim2 = await dagStore.TryClaimNextReadyStepAsync(executionId, "worker-2");

                Assert.Null(claim2);

                var completed = await dagStore.TryCompleteStepAsync(
                    executionId,
                    "start",
                    claim1.ClaimToken,
                    AiStepResult.Ok("done"));

                Assert.True(completed);

                var claim3 = await dagStore.TryClaimNextReadyStepAsync(executionId, "worker-3");

                Assert.NotNull(claim3);
                Assert.Equal("merge", claim3!.StepName);
            }
            finally
            {
                await CleanupDagExecutionAsync(executionId);
            }
        }

        [RedisFact]
        public async Task RecoverTimedOutStepsAsync_Should_Requeue_Expired_Running_Step()
        {
            var dagStore = CreateDagStore();
            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "dag",
                ExecutionMode = AiExecutionMode.Dag,
                Status = AiExecutionStatus.Running
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId
            };

            state.Steps["start"] = new AiStepState
            {
                StepName = "start",
                Status = AiStepExecutionStatus.Running,
                ClaimedBy = "worker-old",
                ClaimToken = "claim-old",
                ClaimedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-9),
                ClaimTimeoutSeconds = 30
            };

            await dagStore.CreateAsync(record, state);

            try
            {
                var recovered = await dagStore.RecoverTimedOutStepsAsync(executionId);

                Assert.Equal(1, recovered);

                var snapshot = await dagStore.GetStateAsync(executionId);

                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["start"];

                Assert.Equal(AiStepExecutionStatus.Ready, step.Status);
                Assert.Null(step.ClaimedBy);
                Assert.Null(step.ClaimToken);
                Assert.Null(step.ClaimedAtUtc);
                Assert.Null(step.LeaseExpiresAtUtc);
                Assert.Equal(1, step.RecoveryCount);
            }
            finally
            {
                await CleanupDagExecutionAsync(executionId);
            }
        }

        [RedisFact]
        public async Task TryCompleteStepAsync_Should_Reject_Wrong_ClaimToken()
        {
            var dagStore = CreateDagStore();
            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "dag",
                ExecutionMode = AiExecutionMode.Dag,
                Status = AiExecutionStatus.Running
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId
            };

            state.Steps["start"] = new AiStepState
            {
                StepName = "start",
                Status = AiStepExecutionStatus.Ready
            };

            await dagStore.CreateAsync(record, state);

            try
            {
                var claim = await dagStore.TryClaimNextReadyStepAsync(executionId, "worker-1");

                Assert.NotNull(claim);

                var completed = await dagStore.TryCompleteStepAsync(
                    executionId,
                    "start",
                    "wrong-token",
                    AiStepResult.Ok("done"));

                Assert.False(completed);
            }
            finally
            {
                await CleanupDagExecutionAsync(executionId);
            }
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_Random_100_Step_Dag()
        {
            await RunGeneratedDagScenarioAsync(
                pipelineName: "dag-random-100",
                stepCount: 100,
                seed: 12345,
                mode: GeneratedDagMode.Random);
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_Parallel_Heavy_100_Step_Dag()
        {
            await RunGeneratedDagScenarioAsync(
                pipelineName: "dag-parallel-heavy-100",
                stepCount: 100,
                seed: 42,
                mode: GeneratedDagMode.ParallelHeavy);
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_Linear_100_Step_Dag()
        {
            await RunGeneratedDagScenarioAsync(
                pipelineName: "dag-linear-100",
                stepCount: 100,
                seed: 7,
                mode: GeneratedDagMode.Linear);
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_FanIn_100_Step_Dag()
        {
            await RunGeneratedDagScenarioAsync(
                pipelineName: "dag-fanin-100",
                stepCount: 100,
                seed: 99,
                mode: GeneratedDagMode.FanIn);
        }

        private async Task RunGeneratedDagScenarioAsync(
     string pipelineName,
     int stepCount,
     int seed,
     GeneratedDagMode mode)
        {
            var definition = CreateGeneratedDagPipeline(
                pipelineName,
                stepCount,
                seed,
                mode);

            var filePath = WritePipelineDefinitionToConfig(definition);

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

            var dataPolicy = new InlineAiExecutionDataPolicy();
            var stepExecutor = new AiStepExecutor(classifier, logger);

            var services = new ServiceCollection();

            services.AddAiStepsFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly,
                typeof(AiDagExecutionEngineRedisIntegrationTests).Assembly);

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<IAiStepRegistry>();
            var resolver = new AiPipelineResolver(registry);
            var sourceSelector = CreateJsonSourceSelector(pipelineName + ".json");

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

            var metrics = ObservabilityFactory.Create();

            var payloadStoreOptions = Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = "mongo",
                RequireReplaySafePayloads = true,
                MaxInlineSizeBytes = 1024,
                Mongo = new MongoAiPayloadStoreOptions
                {
                    Enabled = true,
                    ConnectionString = "mongodb://localhost:27017",
                    DatabaseName = "multiplexed_ai_tests",
                    CollectionName = $"payloads_generated_dag_{Guid.NewGuid():N}"
                }
            });

            var metricsPayload = new InMemoryAiPayloadMetrics();
            var payloadCompactor = new DefaultAiStepResultPayloadCompactor(
                dataPolicy,
                metricsPayload);

            var payloadStore = new MongoAiPayloadStore(payloadStoreOptions);
            var payloadStoreResolver = new FixedAiPayloadStoreResolver(payloadStore);

            var stepPayloadStore = new DefaultAiStepPayloadStore(payloadStoreResolver);
            var stepPayloadIndexStore = new MongoAiStepPayloadIndexStore(payloadStoreOptions);

            var stepResolver = new DefaultAiExecutionStepResolver(
                stepPayloadIndexStore,
                stepPayloadStore);

            var stateWriter = new DefaultAiExecutionStateWriter();
            var stateReader = new DefaultAiExecutionStateReader(new NoopPayloadResolver());

            var retentionPolicy = CreateDisabledRetentionPolicy();
            var retryAdapter = AiRetryTestFactory.CreateRetryAdapter();

            var retentionService = CreateRetentionService(
                payloadCompactor,
                payloadStoreResolver,
                stepPayloadIndexStore);

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
                    stateWriter),
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
                retryAdapter,
                dagStore);

            var engine = new AiDagExecutionEngine(engineServices);

            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync(pipelineName, "Marco");

            try
            {
                var finalRecord = await engine.ExecuteAllAsync(created.ExecutionId);

                Assert.NotNull(finalRecord);
                Assert.Equal(AiExecutionMode.Dag, finalRecord.ExecutionMode);
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
                Assert.Equal(stepCount, finalRecord.CompletedSteps.Count);

                var dagStore2 = GetRequiredService<IAiDagExecutionStore>(engine);
                var state = await dagStore2.GetStateAsync(finalRecord.ExecutionId);

                Assert.NotNull(state);
                Assert.Equal(stepCount, state!.Steps.Count);

                Assert.All(
                    state.Steps.Values,
                    step => Assert.Equal(AiStepExecutionStatus.Completed, step.Status));
            }
            finally
            {
                await CleanupDagExecutionAsync(created.ExecutionId);

                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // Best-effort cleanup only.
                }
            }
        }

        private static AiPipelineDefinition CreateGeneratedDagPipeline(
            string pipelineName,
            int stepCount,
            int seed,
            GeneratedDagMode mode)
        {
            var random = new Random(seed);
            var steps = new List<AiPipelineStepDefinition>();

            for (var i = 0; i < stepCount; i++)
            {
                var name = $"s{i + 1:000}";
                var dependsOn = new List<string>();

                if (i > 0)
                {
                    var previous = steps.Select(x => x.Name).ToList();

                    switch (mode)
                    {
                        case GeneratedDagMode.Linear:
                            dependsOn.Add(previous[^1]);
                            break;

                        case GeneratedDagMode.FanIn:
                            if (i == stepCount - 1)
                            {
                                dependsOn.AddRange(previous);
                            }
                            else
                            {
                                var depCountFanIn = random.Next(0, Math.Min(3, previous.Count) + 1);

                                dependsOn = previous
                                    .OrderBy(_ => random.Next())
                                    .Take(depCountFanIn)
                                    .ToList();
                            }

                            break;

                        case GeneratedDagMode.ParallelHeavy:
                            {
                                var depCountParallel = random.Next(0, Math.Min(1, previous.Count) + 1);

                                dependsOn = previous
                                    .OrderBy(_ => random.Next())
                                    .Take(depCountParallel)
                                    .ToList();

                                break;
                            }

                        case GeneratedDagMode.Random:
                        default:
                            {
                                var depCountRandom = random.Next(0, Math.Min(3, previous.Count) + 1);

                                dependsOn = previous
                                    .OrderBy(_ => random.Next())
                                    .Take(depCountRandom)
                                    .ToList();

                                break;
                            }
                    }
                }

                steps.Add(new AiPipelineStepDefinition
                {
                    Name = name,
                    StepKey = "hello-world",
                    Order = i + 1,
                    DependsOn = dependsOn,
                    Config = new Dictionary<string, object?>
                    {
                        ["delayMs"] = random.Next(0, 10)
                    }
                });
            }

            return new AiPipelineDefinition
            {
                Name = pipelineName,
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = steps
            };
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

        private enum GeneratedDagMode
        {
            Random,
            ParallelHeavy,
            Linear,
            FanIn
        }

        private AiDagExecutionEngine CreateEngine()
        {
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
                typeof(AiDagExecutionEngineRedisIntegrationTests).Assembly);

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

            var metrics = ObservabilityFactory.Create();

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
                    CollectionName = $"payloads_dag_{Guid.NewGuid():N}"
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

            var stateWriter = new DefaultAiExecutionStateWriter();
            var stateReader = new DefaultAiExecutionStateReader(new NoopPayloadResolver());

            var retentionPolicy = CreateDisabledRetentionPolicy();

            var retentionService = CreateRetentionService(
                payloadCompactor,
                payloadStoreResolver,
                stepPayloadIndexStore);

            var retryAdapter = AiRetryTestFactory.CreateRetryAdapter();

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
                    stateWriter),
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
                retryAdapter,   
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
            var metrics = MetricsFactory.Create();
            var keyBuilder = new AiExecutionKeyBuilder();

            var normalizers = new DefaultAiStepResultNormalizerPipeline([new RagStepResultNormalizer()]);

            return new RedisAiDagExecutionStore(
                _connection,
                keyBuilder,
                logger,
                metrics,
                normalizers);
        }

        private static IAiExecutionRetentionService CreateRetentionService(
            IAiStepResultPayloadCompactor payloadCompactor,
            IAiPayloadStoreResolver payloadStoreResolver,
            IAiStepPayloadIndexStore stepPayloadIndexStore)
        {
            ArgumentNullException.ThrowIfNull(payloadCompactor);
            ArgumentNullException.ThrowIfNull(payloadStoreResolver);
            ArgumentNullException.ThrowIfNull(stepPayloadIndexStore);

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

            var stepPayloadStore = new DefaultAiStepPayloadStore(payloadStoreResolver);

            var metrics = new InMemoryAiExecutionRetentionServiceMetrics();

            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var service = new AiExecutionRetentionService(
                policyResolver,
                stepPayloadStore,
                stepPayloadIndexStore,
                payloadCompactor,
                metrics, decisionService);

            return service;
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

        private static IAiPipelineDefinitionSourceSelector CreateJsonSourceSelector(
            string fileName = "dag-parallel-basic.json")
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

        private static IServiceProvider CreateServiceProvider(
            ExecutionContextAccessor accessor,
            IAiExecutionStore store,
            IAiDagExecutionStore dagStore,
            IAiExecutionStateRetentionPolicy retentionPolicy,
            IAiExecutionStateReader stateReader,
            IAiExecutionStateWriter stateWriter)
        {
            return new TestServiceProvider(new Dictionary<Type, object>
            {
                [typeof(ExecutionContextAccessor)] = accessor,
                [typeof(IAiExecutionStore)] = store,
                [typeof(IAiDagExecutionStore)] = dagStore,
                [typeof(IAiExecutionStateRetentionPolicy)] = retentionPolicy,
                [typeof(IAiExecutionStateReader)] = stateReader,
                [typeof(IAiExecutionStateWriter)] = stateWriter
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
                .GetProperty(
                    "Services",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

            var provider = (IServiceProvider)property!.GetValue(engine)!;

            return (T)(provider.GetService(typeof(T))
                ?? throw new InvalidOperationException(
                    $"Required service '{typeof(T).FullName}' is not registered."));
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
                {
                    continue;
                }

                await db.KeyDeleteAsync($"ai:execution:step:{executionId}:{stepName}");
            }

            await db.KeyDeleteAsync(recordKey);
            await db.KeyDeleteAsync(stateKey);
            await db.KeyDeleteAsync(stepsIndexKey);
        }

        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this Redis DAG integration test.");
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
                return _services.TryGetValue(serviceType, out var service)
                    ? service
                    : null;
            }
        }
    }
}