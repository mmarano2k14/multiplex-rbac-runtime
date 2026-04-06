using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Retry;
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
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-fail-once", "Marco");

            try
            {
                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);
                var state = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(state);

                var step = state!.GetOrCreateStep("retry-step");

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
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-delay", "Marco");

            try
            {
                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                // Immediate second execution should not bypass the retry window.
                var second = await engine.ExecuteNextAsync(created.ExecutionId);

                Assert.True(
                    second.Status == AiExecutionStatus.Waiting ||
                    second.Status == AiExecutionStatus.Running);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);
                var state = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(state);

                var step = state!.GetOrCreateStep("retry-step");

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
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-fail-once", "Marco");

            try
            {
                // First attempt fails and should schedule retry.
                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);

                var beforeRetry = await dagStore.GetStateAsync(created.ExecutionId);
                Assert.NotNull(beforeRetry);

                var retryStep = beforeRetry!.GetOrCreateStep("retry-step");

                Console.WriteLine($"Status={retryStep.Status}");
                Console.WriteLine($"RetryCount={retryStep.RetryCount}");
                Console.WriteLine($"MaxRetries={retryStep.MaxRetries}");
                Console.WriteLine($"NextRetryAtUtc={retryStep.NextRetryAtUtc}");
                Console.WriteLine($"Error={retryStep.Error}");

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, retryStep.Status);
                Assert.NotNull(retryStep.NextRetryAtUtc);

                await WaitForRetryWindowAsync(dagStore, created.ExecutionId, "retry-step");

                // Second attempt should execute the retried step and complete it.
                await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                var finalState = await dagStore.GetStateAsync(created.ExecutionId);
                Assert.NotNull(finalState);

                var finalStep = finalState!.GetOrCreateStep("retry-step");





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
            accessor.Set(CreateRuntimeContext());

            var created = await engine.CreateAsync("dag-retry-always-fail", "Marco");

            try
            {
                var dagStore = GetRequiredService<IAiDagExecutionStore>(engine);

                // Drive the execution until the step reaches terminal failure.
                for (var i = 0; i < 10; i++)
                {
                    await ExecuteIgnoringFailureAsync(engine, created.ExecutionId);

                    var state = await dagStore.GetStateAsync(created.ExecutionId);
                    Assert.NotNull(state);

                    var step = state!.GetOrCreateStep("retry-step");

                    Console.WriteLine($"RetryDelay = {step.RetryDelay}");
                    Console.WriteLine($"MaxRetries = {step.MaxRetries}");
                    Console.WriteLine($"RetryCount = {step.RetryCount}");
                    Console.WriteLine($"NextRetryAtUtc = {step.NextRetryAtUtc}");

                    if (step.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        await WaitForRetryWindowAsync(dagStore, created.ExecutionId, "retry-step");
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

                var finalStep = finalState!.GetOrCreateStep("retry-step");

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

        // ------------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------------

        /// <summary>
        /// Executes one engine step and swallows runtime exceptions.
        ///
        /// This is useful for retry-oriented tests where the expected behavior
        /// is persisted state mutation rather than caller-visible exception semantics.
        /// </summary>
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

        /// <summary>
        /// Waits until the retry window for the specified step is open.
        ///
        /// This helper keeps the test deterministic without mutating the persisted execution bundle.
        /// </summary>
        private static async Task WaitForRetryWindowAsync(
            IAiDagExecutionStore dagStore,
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 50; i++)
            {
                var state = await dagStore.GetStateAsync(executionId, cancellationToken);
                if (state is null)
                    throw new InvalidOperationException($"State '{executionId}' was not found.");

                var step = state.GetOrCreateStep(stepName);

                // If no retry time defined → allow execution
                if (!step.NextRetryAtUtc.HasValue)
                {
                    return;
                }

                var delay = step.NextRetryAtUtc.Value - DateTime.UtcNow;

                // Retry window is open
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

        // ------------------------------------------------------------------
        // PIPELINE DEFINITIONS
        // ------------------------------------------------------------------

        /// <summary>
        /// Writes JSON pipeline definitions used by retry-focused integration tests.
        ///
        /// IMPORTANT:
        /// Replace the StepKey values below if your registered test steps use different names.
        /// </summary>
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
                        Execution = new AiPipelineStepExecutionDefinition  {
                            RetryDelayMs = 1000,
                            MaxRetries = 2,
                        },
              
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
                        Execution = new AiPipelineStepExecutionDefinition  {
                            RetryDelayMs = 500,
                            MaxRetries = 1,
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
                        Execution = new AiPipelineStepExecutionDefinition  {
                            RetryDelayMs = 1000,
                            MaxRetries = 3
                        }
                    }
                ]
            });
        }

        // ------------------------------------------------------------------
        // ENGINE / STORE FACTORIES
        // ------------------------------------------------------------------

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

            var cleanupOptions = Options.Create(new AiExecutionCleanupOptions
            {
                AutoCleanupOnCompleted = false,
                AutoCleanupOnFailed = false,
                SuppressCleanupExceptions = true
            });

            return new AiDagExecutionEngine(
                executionStore,
                contextStore,
                accessor,
                contextFactory,
                CreateServiceProvider(accessor, executionStore, dagStore),
                pipelineExecutor,
                logger,
                cleanupService,
                cleanupOptions,
                dagStore);
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
            var keyBuilder = new AiExecutionKeyBuilder();
            return new RedisAiDagExecutionStore(_connection, keyBuilder);
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
            IAiDagExecutionStore dagStore)
        {
            return new TestServiceProvider(new Dictionary<Type, object>
            {
                [typeof(ExecutionContextAccessor)] = accessor,
                [typeof(IAiExecutionStore)] = store,
                [typeof(IAiDagExecutionStore)] = dagStore
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