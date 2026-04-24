using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.DI;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Pipeline.Steps.Test;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Multiplexed.Rbac.Core.Stores;
using Multiplexed.Rbac.Core.Stores.Cache;
using Multiplexed.Rbac.Core.Stores.Memory;
using Multiplexed.Realtime.DI;
using StackExchange.Redis;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Distributed DAG convergence and finalization hardening tests.
    ///
    /// PURPOSE:
    /// Validates that global execution projection remains deterministic and safe
    /// under retry-aware distributed DAG execution.
    ///
    /// THIS TEST SUITE PROVES:
    /// - waiting-for-retry does not prematurely finalize the execution
    /// - terminal failure is projected only when no forward progress remains
    /// - no active or retryable work remains after terminal convergence
    /// - the global execution record remains consistent with authoritative step state
    /// </summary>
    [Collection("redis")]
    public sealed class AiDagExecutionEngineRedisConvergenceHardeningTests
    {
        private readonly IConnectionMultiplexer _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionEngineRedisConvergenceHardeningTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared Redis fixture.</param>
        public AiDagExecutionEngineRedisConvergenceHardeningTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            _connection = fixture.Connection;
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Not_Finalize_While_Step_Is_WaitingForRetry()
        {
            var provider = CreateTestServiceProvider("multi-worker-retry-hardcore.json");

            var engine = provider.GetRequiredService<AiDagExecutionEngine>();
            var dagStore = provider.GetRequiredService<IAiDagExecutionStore>();
            var accessor = provider.GetRequiredService<ExecutionContextAccessor>();

            accessor.Set(CreateRuntimeContext());

            var record = await engine.CreateAsync(
                pipelineName: "multi-worker-retry-hardcore",
                input: "test");

            try
            {
                try
                {
                    await engine.ExecuteNextAsync(record.ExecutionId);
                }
                catch
                {
                    // Expected:
                    // the test step intentionally fails so the execution can move
                    // into the retry-aware waiting state.
                }

                var persistedRecord = await dagStore.GetRecordAsync(record.ExecutionId);
                var persistedState = await dagStore.GetStateAsync(record.ExecutionId);

                Assert.NotNull(persistedRecord);
                Assert.NotNull(persistedState);

                var step = persistedState!.Steps["start"];

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step.Status);
                Assert.Equal(AiExecutionStatus.Waiting, persistedRecord!.Status);

                Assert.False(persistedRecord.IsTerminal);
                Assert.True(step.NextRetryAtUtc.HasValue);
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
                await provider.DisposeAsync();
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Converge_To_Failed_Only_When_No_Progress_Remains()
        {
            var provider = CreateTestServiceProvider("multi-worker-retry-hardcore.json");

            var engine = provider.GetRequiredService<AiDagExecutionEngine>();
            var dagStore = provider.GetRequiredService<IAiDagExecutionStore>();
            var accessor = provider.GetRequiredService<ExecutionContextAccessor>();

            accessor.Set(CreateRuntimeContext());

            var record = await engine.CreateAsync(
                pipelineName: "multi-worker-retry-hardcore",
                input: "test");

            try
            {
                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        await engine.ExecuteNextAsync(record.ExecutionId);
                    }
                    catch
                    {
                        // Expected:
                        // the step fails by design and consumes retry budget progressively.
                    }

                    var state = await dagStore.GetStateAsync(record.ExecutionId);
                    Assert.NotNull(state);

                    var step = state!.Steps["start"];

                    if (step.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        await WaitUntilRetryWindowOpensAsync(
                            dagStore,
                            record.ExecutionId,
                            "start");
                    }
                }

                var finalRecord = await WaitForTerminalRecordAsync(engine, record.ExecutionId);
                var finalState = await dagStore.GetStateAsync(record.ExecutionId);

                Assert.NotNull(finalState);
                Assert.Equal(AiExecutionStatus.Failed, finalRecord.Status);
                Assert.Equal(AiStepExecutionStatus.Failed, finalState!.Steps["start"].Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
                await provider.DisposeAsync();
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Not_Leave_Active_Work_After_Terminal_Failure()
        {
            var provider = CreateTestServiceProvider("multi-worker-retry-hardcore.json");

            var engine = provider.GetRequiredService<AiDagExecutionEngine>();
            var dagStore = provider.GetRequiredService<IAiDagExecutionStore>();
            var accessor = provider.GetRequiredService<ExecutionContextAccessor>();

            accessor.Set(CreateRuntimeContext());

            var record = await engine.CreateAsync(
                pipelineName: "multi-worker-retry-hardcore",
                input: "test");

            try
            {
                var terminal = await WaitForTerminalFailureAsync(
                    engine,
                    dagStore,
                    record.ExecutionId);

                var finalState = await dagStore.GetStateAsync(record.ExecutionId);
                var finalRecord = await dagStore.GetRecordAsync(record.ExecutionId);

                Assert.NotNull(finalState);
                Assert.NotNull(finalRecord);

                Assert.Equal(AiExecutionStatus.Failed, terminal.Status);
                Assert.Equal(AiExecutionStatus.Failed, finalRecord!.Status);

                Assert.DoesNotContain(
                    finalState!.Steps.Values,
                    x => x.Status == AiStepExecutionStatus.Running ||
                         x.Status == AiStepExecutionStatus.Ready ||
                         x.Status == AiStepExecutionStatus.WaitingForRetry ||
                         x.Status == AiStepExecutionStatus.None);
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
                await provider.DisposeAsync();
            }
        }

        // ==============================
        // HELPERS
        // ==============================

        /// <summary>
        /// Waits until the target step has a retry window that is due.
        ///
        /// This helper intentionally polls persisted distributed state rather than
        /// relying on local timing assumptions.
        /// </summary>
        private static async Task WaitUntilRetryWindowOpensAsync(
            IAiDagExecutionStore dagStore,
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 100; i++)
            {
                var state = await dagStore.GetStateAsync(executionId, cancellationToken);
                if (state is null)
                {
                    throw new InvalidOperationException($"State '{executionId}' was not found.");
                }

                var step = state.GetOrCreateStep(stepName);

                if (!step.NextRetryAtUtc.HasValue)
                {
                    return;
                }

                var delay = step.NextRetryAtUtc.Value - DateTime.UtcNow;
                if (delay <= TimeSpan.Zero)
                {
                    return;
                }

                var wait = delay < TimeSpan.FromMilliseconds(50)
                    ? delay
                    : TimeSpan.FromMilliseconds(50);

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken);
                }
            }

            throw new TimeoutException(
                $"Retry window for step '{stepName}' in execution '{executionId}' did not open in time.");
        }

        /// <summary>
        /// Repeatedly calls <see cref="AiDagExecutionEngine.ExecuteNextAsync(string, CancellationToken)"/>
        /// until the global execution record reaches a terminal state or the retry limit is exceeded.
        ///
        /// This is used because the distributed step state is the source of truth,
        /// while the execution record is a projected summary that may lag briefly
        /// under optimistic concurrency races.
        /// </summary>
        private static async Task<AiExecutionRecord> WaitForTerminalRecordAsync(
            AiDagExecutionEngine engine,
            string executionId,
            CancellationToken cancellationToken = default)
        {
            AiExecutionRecord? record = null;

            for (var i = 0; i < 20; i++)
            {
                record = await engine.ExecuteNextAsync(executionId, cancellationToken);

                if (record.IsTerminal)
                {
                    return record;
                }

                await Task.Delay(50, cancellationToken);
            }

            throw new TimeoutException(
                $"Execution '{executionId}' did not converge to a terminal record in time.");
        }

        /// <summary>
        /// Drives the retry-only test pipeline until it reaches terminal failure.
        ///
        /// This helper is used by convergence hardening tests that require a stable
        /// terminal DAG projection while preserving the real distributed retry flow.
        /// </summary>
        private static async Task<AiExecutionRecord> WaitForTerminalFailureAsync(
            AiDagExecutionEngine engine,
            IAiDagExecutionStore dagStore,
            string executionId,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var record = await engine.ExecuteNextAsync(executionId, cancellationToken);

                    if (record.IsTerminal)
                    {
                        return record;
                    }
                }
                catch
                {
                    // Expected:
                    // the retry test step intentionally fails so the execution can
                    // progress through waiting-for-retry and terminal failure states.
                }

                var state = await dagStore.GetStateAsync(executionId, cancellationToken);
                if (state is null)
                {
                    throw new InvalidOperationException($"State '{executionId}' was not found.");
                }

                var step = state.GetOrCreateStep("start");

                if (step.Status == AiStepExecutionStatus.WaitingForRetry)
                {
                    await WaitUntilRetryWindowOpensAsync(
                        dagStore,
                        executionId,
                        "start",
                        cancellationToken);
                }
            }

            throw new TimeoutException(
                $"Execution '{executionId}' did not converge to terminal failure in time.");
        }

        /// <summary>
        /// Creates a test service provider using the real runtime DI and Redis fixture connection.
        /// </summary>
        private ServiceProvider CreateTestServiceProvider(string pipelineFileName)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiEngine:DefaultPipelineDefinitionSource"] = "Json",
                    ["AiEngine:JsonPipelineDefinitionFilePath"] = $"config/{pipelineFileName}",
                    ["AiExecutionCleanup:AutoCleanupOnCompleted"] = "false",
                    ["AiExecutionCleanup:AutoCleanupOnFailed"] = "false",
                    ["AiExecutionCleanup:SuppressCleanupExceptions"] = "true"
                })
                .Build();

            var services = new ServiceCollection();

            services.AddMemoryCache();
            services.AddOptions();


            services.AddLogging(builder =>
            {
                builder.ClearProviders();
            });

            services.AddSingleton<IConnectionMultiplexer>(_connection);

            services.AddSingleton<TestStepAttemptTracker>();

            services.AddSingleton<ExecutionContextAccessor>();
            services.AddSingleton<IExecutionContextAccessor>(sp => sp.GetRequiredService<ExecutionContextAccessor>());
            services.AddSingleton<IExecutionContextFactory, ExecutionContextFactory>();

            var contextOptions = Options.Create(new ContextRuntimeOptions
            {
                UseRedisLuaScriptShaCaching = true
            });

            services.AddSingleton(contextOptions);

            services.AddSingleton<IContextStore>(sp =>
            {
                var redisContextStore = new RedisContextStore(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    contextOptions);

                var memoryContextStore = new MemoryContextStore(
                    new MemoryCache(new MemoryCacheOptions()),
                    TimeSpan.FromMinutes(5));

                return new CompositeContextStore(redisContextStore, memoryContextStore);
            });

            services.AddMultiplexAI(configuration);

            services.AddMultiplexRealtime()
                .AddSignalRRealtimeTransport(options =>
                {
                    options.CorsPolicy = "SignalRCors";
                    options.AllowedOrigins =
                    [
                        "http://localhost:3000"
                    ];
                });

            services.AddAiStepsFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly,
                typeof(AiDagExecutionEngineRedisConvergenceHardeningTests).Assembly);

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Creates a deterministic runtime RBAC context for integration tests.
        /// </summary>
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

        /// <summary>
        /// Deletes the Redis execution bundle created by the test.
        /// </summary>
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
    }
}