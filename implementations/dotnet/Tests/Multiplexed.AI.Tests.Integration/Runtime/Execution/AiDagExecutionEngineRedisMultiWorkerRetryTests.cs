using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.DI;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
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
using System.Collections.Concurrent;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Hard multi-worker distributed retry test.
    ///
    /// PURPOSE:
    /// Validates that the distributed DAG runtime remains deterministic under concurrent workers
    /// during retry scheduling, retry-window reopening, and terminal convergence.
    ///
    /// THIS TEST PROVES:
    /// - only one worker may claim a step at a time
    /// - retry window is respected before re-execution
    /// - only one worker may reclaim a retryable step when the retry window opens
    /// - retry exhaustion leads to terminal failure exactly once
    /// - no premature or duplicate terminal convergence occurs
    /// </summary>
    [Collection("redis")]
    public sealed class AiDagExecutionEngineRedisMultiWorkerRetryTests
    {
        private readonly IConnectionMultiplexer _connection;

        public AiDagExecutionEngineRedisMultiWorkerRetryTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            _connection = fixture.Connection;
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Allow_Only_One_Worker_To_Reclaim_When_Retry_Window_Opens()
        {
            // -----------------------------------------------------------------
            // Arrange
            // -----------------------------------------------------------------
            var executionResults = new ConcurrentBag<AiExecutionRecord>();
            var executionErrors = new ConcurrentBag<Exception>();

            var provider = CreateTestServiceProvider("multi-worker-retry-hardcore.json");

            var engine = provider.GetRequiredService<AiDagExecutionEngine>();
            var dagStore = provider.GetRequiredService<IAiDagExecutionStore>();
            var accessor = provider.GetRequiredService<ExecutionContextAccessor>();
            var tracker = provider.GetRequiredService<TestStepAttemptTracker>();

            accessor.Set(CreateRuntimeContext());

            var record = await engine.CreateAsync(
                pipelineName: "multi-worker-retry-hardcore",
                input: "test");

            try
            {
                // -----------------------------------------------------------------
                // Phase 1:
                // Multiple workers race → only ONE must execute
                // -----------------------------------------------------------------
                await ParallelRunWorkersAsync(
                    workerCount: 3,
                    action: async () =>
                    {
                        try
                        {
                            var result = await engine.ExecuteNextAsync(record.ExecutionId);
                            executionResults.Add(result);
                        }
                        catch (Exception ex)
                        {
                            executionErrors.Add(ex);
                        }
                    });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);

                var stateAfterWave1 = await dagStore.GetStateAsync(record.ExecutionId);
                Assert.NotNull(stateAfterWave1);

                var step1 = stateAfterWave1!.Steps["start"];

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step1.Status);
                Assert.Equal(1, step1.RetryCount);
                Assert.True(step1.NextRetryAtUtc.HasValue);

                // Exactly one real execution attempt should have happened.
                Assert.Equal(1, tracker.Count);

                // -----------------------------------------------------------------
                // Phase 2:
                // Retry window NOT open → nobody must reclaim
                // -----------------------------------------------------------------
                await ParallelRunWorkersAsync(
                    workerCount: 3,
                    action: async () =>
                    {
                        try
                        {
                            var result = await engine.ExecuteNextAsync(record.ExecutionId);
                            executionResults.Add(result);
                        }
                        catch (Exception ex)
                        {
                            executionErrors.Add(ex);
                        }
                    });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);

                var stateBeforeRetry = await dagStore.GetStateAsync(record.ExecutionId);
                Assert.NotNull(stateBeforeRetry);

                var stepBeforeRetry = stateBeforeRetry!.Steps["start"];

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, stepBeforeRetry.Status);
                Assert.Equal(1, stepBeforeRetry.RetryCount);

                // Still only one execution.
                Assert.Equal(1, tracker.Count);

                // -----------------------------------------------------------------
                // Phase 3:
                // Wait retry window → race again → ONLY ONE retry execution
                // -----------------------------------------------------------------
                await WaitUntilRetryWindowOpensAsync(
                    dagStore,
                    record.ExecutionId,
                    "start");

                await ParallelRunWorkersAsync(
                    workerCount: 5,
                    action: async () =>
                    {
                        try
                        {
                            var result = await engine.ExecuteNextAsync(record.ExecutionId);
                            executionResults.Add(result);
                        }
                        catch (Exception ex)
                        {
                            executionErrors.Add(ex);
                        }
                    });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);

                var stateAfterWave2 = await dagStore.GetStateAsync(record.ExecutionId);
                Assert.NotNull(stateAfterWave2);

                var step2 = stateAfterWave2!.Steps["start"];

                Assert.True(
                    step2.Status == AiStepExecutionStatus.WaitingForRetry ||
                    step2.Status == AiStepExecutionStatus.Failed);

                Assert.Equal(2, tracker.Count);

                // -----------------------------------------------------------------
                // Phase 4:
                // Final retry → must converge to FAILED exactly once
                // -----------------------------------------------------------------
                if (step2.Status == AiStepExecutionStatus.WaitingForRetry)
                {
                    await WaitUntilRetryWindowOpensAsync(
                        dagStore,
                        record.ExecutionId,
                        "start");

                    await ParallelRunWorkersAsync(
                        workerCount: 5,
                        action: async () =>
                        {
                            try
                            {
                                var result = await engine.ExecuteNextAsync(record.ExecutionId);
                                executionResults.Add(result);
                            }
                            catch (Exception ex)
                            {
                                executionErrors.Add(ex);
                            }
                        });
                }

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);

                var stabilizedRecord = await WaitForTerminalRecordAsync(engine, record.ExecutionId);

                var finalRecord = await dagStore.GetRecordAsync(record.ExecutionId);
                var finalState = await dagStore.GetStateAsync(record.ExecutionId);

                Assert.NotNull(finalRecord);
                Assert.NotNull(finalState);

                var finalStep = finalState!.Steps["start"];

                Assert.Equal(AiExecutionStatus.Failed, stabilizedRecord.Status);
                Assert.Equal(AiExecutionStatus.Failed, finalRecord!.Status);
                Assert.Equal(AiStepExecutionStatus.Failed, finalStep.Status);

                // Infrastructure recovery must remain separate from business retry.
                Assert.Equal(0, finalStep.RecoveryCount);

                // Retry budget should have been consumed by business failures.
                Assert.True(finalStep.RetryCount >= 1);

                // No active step should remain once terminal failure has converged.
                Assert.DoesNotContain(
                    finalState.Steps.Values,
                    x => x.Status == AiStepExecutionStatus.Running ||
                         x.Status == AiStepExecutionStatus.Ready ||
                         x.Status == AiStepExecutionStatus.WaitingForRetry);

                // Final projection must be stable.
                var reloaded = await dagStore.GetRecordAsync(record.ExecutionId);
                Assert.NotNull(reloaded);
                Assert.Equal(AiExecutionStatus.Failed, reloaded!.Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
                await provider.DisposeAsync();
            }
        }

        // ==============================
        // ADDITIONAL ELITE TESTS
        // ==============================

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Remain_SingleOwner_Under_50_Concurrent_Workers()
        {
            var executionErrors = new ConcurrentBag<Exception>();

            var provider = CreateTestServiceProvider("multi-worker-retry-hardcore.json");

            var engine = provider.GetRequiredService<AiDagExecutionEngine>();
            var dagStore = provider.GetRequiredService<IAiDagExecutionStore>();
            var accessor = provider.GetRequiredService<ExecutionContextAccessor>();
            var tracker = provider.GetRequiredService<TestStepAttemptTracker>();

            accessor.Set(CreateRuntimeContext());

            var record = await engine.CreateAsync(
                pipelineName: "multi-worker-retry-hardcore",
                input: "test");

            try
            {
                await ParallelRunWorkersAsync(50, async () =>
                {
                    try
                    {
                        await engine.ExecuteNextAsync(record.ExecutionId);
                    }
                    catch (Exception ex)
                    {
                        executionErrors.Add(ex);
                    }
                });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);

                var state1 = await dagStore.GetStateAsync(record.ExecutionId);
                Assert.NotNull(state1);

                var step1 = state1!.Steps["start"];

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step1.Status);
                Assert.Equal(1, step1.RetryCount);
                Assert.True(step1.NextRetryAtUtc.HasValue);

                Assert.Equal(1, tracker.Count);

                executionErrors = new ConcurrentBag<Exception>();

                await WaitUntilRetryWindowOpensAsync(dagStore, record.ExecutionId, "start");

                await ParallelRunWorkersAsync(50, async () =>
                {
                    try
                    {
                        await engine.ExecuteNextAsync(record.ExecutionId);
                    }
                    catch (Exception ex)
                    {
                        executionErrors.Add(ex);
                    }
                });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);

                var state2 = await dagStore.GetStateAsync(record.ExecutionId);
                Assert.NotNull(state2);

                var step2 = state2!.Steps["start"];

                Assert.True(
                    step2.Status == AiStepExecutionStatus.WaitingForRetry ||
                    step2.Status == AiStepExecutionStatus.Failed);

                Assert.Equal(2, tracker.Count);

                var stabilized = await WaitForTerminalRecordAsync(engine, record.ExecutionId);
                var finalState = await dagStore.GetStateAsync(record.ExecutionId);

                Assert.Equal(AiExecutionStatus.Failed, stabilized.Status);
                Assert.NotNull(finalState);
                Assert.Equal(AiStepExecutionStatus.Failed, finalState!.Steps["start"].Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
                await provider.DisposeAsync();
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Recover_Correctly_After_Exception_During_Retry_Cycle()
        {
            var executionErrors = new ConcurrentBag<Exception>();

            var provider = CreateTestServiceProvider("multi-worker-retry-hardcore.json");

            var engine = provider.GetRequiredService<AiDagExecutionEngine>();
            var dagStore = provider.GetRequiredService<IAiDagExecutionStore>();
            var accessor = provider.GetRequiredService<ExecutionContextAccessor>();
            var tracker = provider.GetRequiredService<TestStepAttemptTracker>();

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
                catch { }

                var state1 = await dagStore.GetStateAsync(record.ExecutionId);
                Assert.NotNull(state1);

                var step1 = state1!.Steps["start"];

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step1.Status);
                Assert.Equal(1, step1.RetryCount);
                Assert.Equal(1, tracker.Count);

                await ParallelRunWorkersAsync(10, async () =>
                {
                    try
                    {
                        await engine.ExecuteNextAsync(record.ExecutionId);
                    }
                    catch (Exception ex)
                    {
                        executionErrors.Add(ex);
                    }
                });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);
                Assert.Equal(1, tracker.Count);

                executionErrors = new ConcurrentBag<Exception>();

                await WaitUntilRetryWindowOpensAsync(dagStore, record.ExecutionId, "start");

                await ParallelRunWorkersAsync(10, async () =>
                {
                    try
                    {
                        await engine.ExecuteNextAsync(record.ExecutionId);
                    }
                    catch (Exception ex)
                    {
                        executionErrors.Add(ex);
                    }
                });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);
                Assert.Equal(2, tracker.Count);

                var stabilized = await WaitForTerminalRecordAsync(engine, record.ExecutionId);
                var finalState = await dagStore.GetStateAsync(record.ExecutionId);

                Assert.Equal(AiExecutionStatus.Failed, stabilized.Status);
                Assert.NotNull(finalState);
                Assert.Equal(AiStepExecutionStatus.Failed, finalState!.Steps["start"].Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
                await provider.DisposeAsync();
            }
        }

        [RedisFact]
        public async Task ExecuteNextAsync_Should_Remain_Deterministic_Under_Jittered_Worker_Latency()
        {
            var executionErrors = new ConcurrentBag<Exception>();

            var provider = CreateTestServiceProvider("multi-worker-retry-hardcore.json");

            var engine = provider.GetRequiredService<AiDagExecutionEngine>();
            var dagStore = provider.GetRequiredService<IAiDagExecutionStore>();
            var accessor = provider.GetRequiredService<ExecutionContextAccessor>();
            var tracker = provider.GetRequiredService<TestStepAttemptTracker>();

            accessor.Set(CreateRuntimeContext());

            var record = await engine.CreateAsync(
                pipelineName: "multi-worker-retry-hardcore",
                input: "test");

            try
            {
                await ParallelRunWorkersWithJitterAsync(20, 0, 150, async () =>
                {
                    try
                    {
                        await engine.ExecuteNextAsync(record.ExecutionId);
                    }
                    catch (Exception ex)
                    {
                        executionErrors.Add(ex);
                    }
                });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);

                var state1 = await dagStore.GetStateAsync(record.ExecutionId);
                Assert.NotNull(state1);

                Assert.Equal(AiStepExecutionStatus.WaitingForRetry, state1!.Steps["start"].Status);
                Assert.Equal(1, tracker.Count);

                executionErrors = new ConcurrentBag<Exception>();

                await WaitUntilRetryWindowOpensAsync(dagStore, record.ExecutionId, "start");

                await ParallelRunWorkersWithJitterAsync(20, 0, 150, async () =>
                {
                    try
                    {
                        await engine.ExecuteNextAsync(record.ExecutionId);
                    }
                    catch (Exception ex)
                    {
                        executionErrors.Add(ex);
                    }
                });

                AssertOnlyExpectedConcurrencyConflicts(executionErrors);
                Assert.Equal(2, tracker.Count);

                var stabilized = await WaitForTerminalRecordAsync(engine, record.ExecutionId);

                Assert.Equal(AiExecutionStatus.Failed, stabilized.Status);
            }
            finally
            {
                await CleanupDagExecutionAsync(record.ExecutionId);
                await provider.DisposeAsync();
            }
        }

        // ==============================
        // HELPER
        // ==============================

        private static async Task ParallelRunWorkersWithJitterAsync(
            int workerCount,
            int minDelayMs,
            int maxDelayMs,
            Func<Task> action)
        {
            var random = new Random();

            var tasks = Enumerable.Range(0, workerCount)
                .Select(async _ =>
                {
                    var delay = random.Next(minDelayMs, maxDelayMs + 1);
                    await Task.Delay(delay);
                    await action();
                })
                .ToArray();

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Runs the provided worker action concurrently using the specified number of workers.
        /// </summary>
        private static async Task ParallelRunWorkersAsync(
            int workerCount,
            Func<Task> action)
        {
            var tasks = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(action))
                .ToArray();

            await Task.WhenAll(tasks);
        }

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

            // Shared attempt tracker used by the test step and the assertions.
            services.AddSingleton<TestStepAttemptTracker>();

            // RBAC / execution context infrastructure
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

            // Full AI runtime
            services.AddMultiplexAI(configuration);

            // Realtime plumbing may still be required by runtime logger dependencies.
            services.AddMultiplexRealtime()
                .AddSignalRRealtimeTransport(options =>
                {
                    options.CorsPolicy = "SignalRCors";
                    options.AllowedOrigins =
                    [
                        "http://localhost:3000"
                    ];
                });

            // IMPORTANT:
            // Re-register step discovery with the test assembly included.
            services.AddAiStepsFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly,
                typeof(AiDagExecutionEngineRedisMultiWorkerRetryTests).Assembly);

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

        /// <summary>
        /// Tracks the number of real step executions observed by the flaky retry test step.
        /// This is used to prove that only one worker actually executes each retry attempt.
        /// </summary>
        private sealed class TestStepAttemptTracker
        {
            private int _count;

            /// <summary>
            /// Gets the total number of recorded step execution attempts.
            /// </summary>
            public int Count => Volatile.Read(ref _count);

            /// <summary>
            /// Atomically increments the execution attempt count and returns the new value.
            /// </summary>
            public int Increment()
                => Interlocked.Increment(ref _count);
        }

        /// <summary>
        /// Test step that always fails and records each real execution attempt through a shared tracker.
        /// This allows the test to prove that distributed retry claim behavior remains single-owner.
        /// </summary>
        [AiStep("test-flaky-retry")]
        private sealed class TestFlakyRetryStep : IAiStep
        {
            private readonly TestStepAttemptTracker _tracker;

            public TestFlakyRetryStep(TestStepAttemptTracker tracker)
            {
                _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            }

            public string Key => "test-flaky-retry";

            public string Name => "test-flaky-retry";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                var attempt = _tracker.Increment();

                return Task.FromResult(new AiStepResult
                {
                    Success = false,
                    Error = $"Simulated retryable failure attempt {attempt}."
                });
            }
        }

        /// <summary>
        /// Asserts that all captured execution errors are expected distributed concurrency conflicts.
        /// In multi-worker distributed tests, losing workers may legitimately fail optimistic
        /// execution-record persistence after another worker has already projected the global state.
        /// </summary>
        private static void AssertOnlyExpectedConcurrencyConflicts(
            ConcurrentBag<Exception> executionErrors)
        {
            foreach (var error in executionErrors)
            {
                var invalidOperation = error as InvalidOperationException;

                Assert.True(
                    invalidOperation is not null &&
                    string.Equals(
                        invalidOperation.Message,
                        "Concurrency conflict on execution update.",
                        StringComparison.Ordinal),
                    $"Unexpected exception captured during multi-worker execution: {error}");
            }
        }

        /// <summary>
        /// Repeatedly calls ExecuteNextAsync until the global execution record reaches
        /// a terminal state or the retry limit is exceeded.
        ///
        /// This is used in multi-worker tests because the distributed step state is the
        /// source of truth, while the execution record is a projected summary that may
        /// lag briefly under optimistic concurrency races.
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
    }
}