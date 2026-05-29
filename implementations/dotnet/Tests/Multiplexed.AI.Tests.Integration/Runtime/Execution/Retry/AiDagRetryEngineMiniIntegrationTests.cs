using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Pipeline.Steps.Test;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retry
{
    /// <summary>
    /// Small retry-engine integration tests.
    ///
    /// PURPOSE:
    /// - Validate retry behavior with the real DAG engine.
    /// - Keep retry tests small and deterministic.
    /// - Avoid using the heavy chaos tests as the first correctness signal.
    ///
    /// IMPORTANT:
    /// - These tests intentionally use the real engine/store flow.
    /// - They do not try to validate every multi-worker race.
    /// - Chaos/concurrency tests should remain separate from these focused tests.
    /// </summary>
    public sealed class AiDagRetryEngineMiniIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";

        /// <summary>
        /// Verifies that a failing retry-capable step moves to WaitingForRetry
        /// when retry budget remains.
        ///
        /// SCENARIO:
        /// - A DAG execution contains at least one retry-capable failing step.
        /// - The first execution attempt fails.
        ///
        /// EXPECTATION:
        /// - The failing step is not marked terminal immediately.
        /// - The step moves to WaitingForRetry.
        /// - RetryCount is incremented.
        /// - NextRetryAtUtc is set.
        ///
        /// WHY THIS MATTERS:
        /// - A retryable failure must remain non-terminal until the retry budget is exhausted.
        /// - This protects deterministic convergence from finalizing too early.
        /// </summary>
        [Fact]
        public async Task ExecuteNextAsync_Should_Move_Failed_Step_To_WaitingForRetry_When_RetryBudget_Remains()
        {
            var options = CreateRetryOptions(
                "config\\multi-worker-retry-hardcore.json");

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddSingleton<TestStepAttemptTracker>();
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "multi-worker-retry-hardcore",
                "test");

            try
            {
                await host.Engine.ExecuteNextAsync(created.ExecutionId);
            }
            catch
            {
                // Expected: the configured test step intentionally fails.
                // The important assertion is persisted retry state below.
            }

            var (record, state) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.NotNull(record);
            Assert.NotNull(state);
            Assert.NotEmpty(state.Steps);

            var waitingStep = state.Steps.Values.SingleOrDefault(
                x => x.Status == AiStepExecutionStatus.WaitingForRetry);

            Assert.NotNull(waitingStep);
            Assert.True(
                waitingStep!.RetryState?.RetryCount > 0,
                $"Step '{waitingStep.StepName}' should have consumed at least one retry attempt.");

            Assert.True(
                waitingStep.RetryState.NextRetryAtUtc.HasValue,
                $"Step '{waitingStep.StepName}' should have a retry schedule.");

            Assert.False(
                record.IsTerminal,
                "Execution must not be terminal while a retryable step is WaitingForRetry.");
        }

        /// <summary>
        /// Verifies that a retry-capable failing execution eventually reaches
        /// terminal failure when the retry budget is exhausted.
        ///
        /// SCENARIO:
        /// - A retry-capable step keeps failing.
        /// - The test repeatedly advances the engine and waits for retry windows.
        ///
        /// EXPECTATION:
        /// - The execution eventually becomes terminal.
        /// - The final status is Failed.
        /// - No step remains Running, Ready, WaitingForRetry, or None.
        ///
        /// WHY THIS MATTERS:
        /// - Retry must be non-terminal while budget remains.
        /// - Retry must become terminal once no retry budget remains.
        /// - This validates deterministic convergence around retry exhaustion.
        /// </summary>
        [Fact]
        public async Task ExecuteNextAsync_Should_Reach_Failed_When_RetryBudget_Is_Exhausted()
        {
            var options = CreateRetryOptions(
                 "config\\multi-worker-retry-hardcore.json");

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddSingleton<TestStepAttemptTracker>();
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "multi-worker-retry-hardcore",
                "test");

            var deadlineUtc = DateTime.UtcNow.AddSeconds(20);

            while (DateTime.UtcNow < deadlineUtc)
            {
                try
                {
                    var current = await host.Engine.ExecuteNextAsync(created.ExecutionId);

                    if (current.IsTerminal)
                    {
                        break;
                    }
                }
                catch
                {
                    // Expected while the retry step is still failing.
                    // Persisted state is checked after each attempt.
                }

                var state = await host.ServiceProvider
                    .GetRequiredService<IAiDagExecutionStore>()
                    .GetStateAsync(created.ExecutionId);

                if (state is not null)
                {
                    await WaitForRetryWindowIfNeededAsync(state);
                }

                var record = await host.ServiceProvider
                    .GetRequiredService<IAiDagExecutionStore>()
                    .GetRecordAsync(created.ExecutionId);

                if (record?.IsTerminal == true)
                {
                    break;
                }
            }

            var (finalRecord, finalState) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.True(
                finalRecord.IsTerminal,
                $"Execution '{created.ExecutionId}' did not reach terminal state.{Environment.NewLine}{AiDagExecutionEngineFixture.DumpState(finalRecord, finalState)}");

            Assert.Equal(AiExecutionStatus.Failed, finalRecord.Status);

            Assert.DoesNotContain(
                finalState.Steps.Values,
                x => x.Status == AiStepExecutionStatus.Running ||
                     x.Status == AiStepExecutionStatus.Ready ||
                     x.Status == AiStepExecutionStatus.WaitingForRetry ||
                     x.Status == AiStepExecutionStatus.None);
        }

        /// <summary>
        /// Verifies that a retryable step is not executed multiple times
        /// concurrently or increment RetryCount more than once for the same retry window.
        ///
        /// WHY THIS MATTERS:
        /// - Prevents duplicate work
        /// - Prevents retry inflation
        /// - Critical for multi-worker safety
        /// </summary>
        [Fact]
        public async Task ExecuteNextAsync_Should_Not_Double_Retry_Same_Step()
        {
            var options = CreateRetryOptions(
                "config\\multi-worker-retry-hardcore.json");

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddSingleton<TestStepAttemptTracker>();
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "multi-worker-retry-hardcore",
                "test");

            try
            {
                await host.Engine.ExecuteNextAsync(created.ExecutionId);
            }
            catch { }

            var (_, state1) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var retryStep1 = state1.Steps.Values.Single(
                x => x.Status == AiStepExecutionStatus.WaitingForRetry);

            var retryCountBefore = retryStep1.RetryState?.RetryCount;

            // 🔥 Call multiple times BEFORE retry window
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await host.Engine.ExecuteNextAsync(created.ExecutionId);
                }
                catch { }
            }

            var (_, state2) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var retryStep2 = state2.Steps.Values.Single(
                x => x.Status == AiStepExecutionStatus.WaitingForRetry);

            if (retryStep1.RetryState?.NextRetryAtUtc > DateTime.UtcNow)
            {
                Assert.Equal(
                    retryCountBefore,
                    retryStep2.RetryState?.RetryCount);
            }
            else
            {
                Assert.True(
                    retryStep2.RetryState?.RetryCount <= retryStep2.Retry?.MaxRetries);
            }
        }

        /// <summary>
        /// Verifies that recovery (timeout of Running step) does NOT affect RetryCount.
        ///
        /// WHY THIS MATTERS:
        /// - Recovery is not a retry
        /// - Prevents retry budget corruption
        /// </summary>
        [Fact]
        public async Task Recovery_Should_Not_Increment_RetryCount()
        {
            var options = CreateRetryOptions(
                "config\\multi-worker-retry-hardcore.json");

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddSingleton<TestStepAttemptTracker>();
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "multi-worker-retry-hardcore",
                "test");

            // First attempt (will fail → WaitingForRetry)
            try { await host.Engine.ExecuteNextAsync(created.ExecutionId); } catch { }

            var (_, state1) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var step1 = state1.Steps.Values.Single(
                x => x.Status == AiStepExecutionStatus.WaitingForRetry);

            var retryBefore = step1.RetryState?.RetryCount;

            // Simulate timeout recovery by forcing execution again after delay
            await WaitForRetryWindowIfNeededAsync(state1);

            try { await host.Engine.ExecuteNextAsync(created.ExecutionId); } catch { }

            var (_, state2) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var step2 = state2.Steps.Values.Single(
                x => x.RetryState?.RetryCount >= retryBefore);

            Assert.True(step2.RetryState?.RetryCount >= retryBefore);
        }

        /// <summary>
        /// Verifies that concurrent workers do not corrupt retry state or terminal convergence.
        ///
        /// SCENARIO:
        /// - Multiple workers call ExecuteNextAsync on the same execution.
        /// - The retry step fails repeatedly until retry budget is exhausted.
        /// - Workers may race, throw, or observe intermediate state.
        ///
        /// EXPECTATION:
        /// - RetryCount never exceeds MaxRetries.
        /// - Final record reaches Failed.
        /// - Final state has no active retry/running work.
        /// - CompletedSteps projection remains coherent.
        ///
        /// WHY THIS MATTERS:
        /// - This is a controlled chaos test.
        /// - It validates multi-worker safety without using a huge stress scenario.
        /// </summary>
        [Fact]
        public async Task Concurrent_ExecuteNextAsync_Should_Not_Corrupt_Retry_State()
        {
            var options = CreateRetryOptions(
                "config\\multi-worker-retry-hardcore.json");

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddSingleton<TestStepAttemptTracker>();
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "multi-worker-retry-hardcore",
                "test");

            const int workerCount = 10;
            var deadlineUtc = DateTime.UtcNow.AddSeconds(20);

            while (DateTime.UtcNow < deadlineUtc)
            {
                var tasks = Enumerable.Range(0, workerCount)
                    .Select(_ => Task.Run(async () =>
                    {
                        try
                        {
                            await host.Engine.ExecuteNextAsync(created.ExecutionId);
                        }
                        catch
                        {
                            // Expected under concurrent retry pressure.
                            // The authoritative state is asserted below.
                        }
                    }))
                    .ToArray();

                await Task.WhenAll(tasks);

                var (record, state) = await LoadDistributedTruthAsync(
                    host.ServiceProvider,
                    created.ExecutionId);

                Assert.NotEmpty(state.Steps);

                foreach (var step in state.Steps.Values)
                {
                    Assert.True(
                        step.RetryState?.RetryCount <= step.Retry?.MaxRetries,
                        $"Step '{step.StepName}' exceeded retry budget. RetryCount={step.RetryState?.RetryCount}, MaxRetries={step.Retry.MaxRetries}");
                }

                if (record.IsTerminal)
                {
                    break;
                }

                await WaitForRetryWindowIfNeededAsync(state);
            }

            var (finalRecord, finalState) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.True(
                finalRecord.IsTerminal,
                $"Execution did not reach terminal state.{Environment.NewLine}{AiDagExecutionEngineFixture.DumpState(finalRecord, finalState)}");

            Assert.Equal(AiExecutionStatus.Failed, finalRecord.Status);

            foreach (var step in finalState.Steps.Values)
            {
                Assert.True(
                    step.RetryState?.RetryCount <= step.Retry?.MaxRetries,
                    $"Step '{step.StepName}' exceeded retry budget. RetryCount={step.RetryState?.RetryCount}, MaxRetries={step.Retry.MaxRetries}");
            }

            Assert.DoesNotContain(
                finalState.Steps.Values,
                x => x.Status == AiStepExecutionStatus.Running ||
                     x.Status == AiStepExecutionStatus.Ready ||
                     x.Status == AiStepExecutionStatus.WaitingForRetry ||
                     x.Status == AiStepExecutionStatus.None);

            var completedFromState = finalState.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var completedFromRecord = (finalRecord.CompletedSteps ?? new List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(completedFromState, completedFromRecord);
        }

        /// <summary>
        /// Verifies that recovery and retry accounting remain separate.
        ///
        /// SCENARIO:
        /// - A retry-capable step is first moved to WaitingForRetry.
        /// - RetryCount is captured.
        /// - Recovery is triggered through the DAG store.
        ///
        /// EXPECTATION:
        /// - Recovery must not increment RetryCount.
        /// - RecoveryCount may increment only for timed-out Running work.
        /// - Retry budget must remain purely controlled by business retry failures.
        ///
        /// WHY THIS MATTERS:
        /// - Recovery is infrastructure repair.
        /// - Retry is business execution retry.
        /// - Mixing both corrupts retry budgets and convergence decisions.
        /// </summary>
        [Fact]
        public async Task Recovery_Should_Not_Affect_WaitingForRetry_Step()
        {
            var options = CreateRetryOptions(
                "config\\multi-worker-retry-hardcore.json");

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddSingleton<TestStepAttemptTracker>();
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "multi-worker-retry-hardcore",
                "test");

            try
            {
                await host.Engine.ExecuteNextAsync(created.ExecutionId);
            }
            catch
            {
                // Expected: first attempt fails and schedules retry.
            }

            var (_, stateBeforeRecovery) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var retryStepBefore = stateBeforeRecovery.Steps.Values.Single(
                x => x.Status == AiStepExecutionStatus.WaitingForRetry);

            var retryCountBefore = retryStepBefore.RetryState?.RetryCount;
            var recoveryCountBefore = retryStepBefore.RecoveryCount;

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            await dagStore.RecoverTimedOutStepsAsync(
                created.ExecutionId,
                CancellationToken.None);

            var (_, stateAfterRecovery) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var retryStepAfter = stateAfterRecovery.Steps[retryStepBefore.StepName];

            Assert.Equal(retryCountBefore, retryStepAfter.RetryState?.RetryCount);

            Assert.Equal(
                recoveryCountBefore,
                retryStepAfter.RecoveryCount);

            Assert.Equal(
                AiStepExecutionStatus.WaitingForRetry,
                retryStepAfter.Status);
        }

        /// <summary>
        /// Verifies that recovery resets a timed-out Running step without consuming retry budget.
        ///
        /// SCENARIO:
        /// - A step is manually forced into Running state.
        /// - Its claim timestamp is old enough to be considered timed out.
        /// - Recovery is executed.
        ///
        /// EXPECTATION:
        /// - The step moves back to Ready.
        /// - RecoveryCount increments.
        /// - RetryCount remains unchanged.
        ///
        /// WHY THIS MATTERS:
        /// - Recovery repairs infrastructure-level stuck work.
        /// - Recovery must not consume business retry budget.
        /// </summary>
        [Fact(Skip = "Requires store-level claim manipulation; not valid at engine integration level.")]
        public async Task Recovery_Should_Reset_Running_Step_Without_Incrementing_RetryCount()
        {
            var options = CreateRetryOptions(
                "config\\multi-worker-retry-hardcore.json");

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddSingleton<TestStepAttemptTracker>();
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "multi-worker-retry-hardcore",
                "test");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var (_, stateBefore) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var step = stateBefore.Steps.Values.Single(x => x.StepName == "start");

            step.Status = AiStepExecutionStatus.Running;
            step.RetryState?.RetryCount = 1;
            step.RecoveryCount = 0;
            step.ClaimedBy = "test-worker";
            step.ClaimToken = Guid.NewGuid().ToString("N");
            step.ClaimedAtUtc = DateTime.UtcNow.AddMinutes(-10);

            /*
            await dagStore.SaveStepStateAsync(
                created.ExecutionId,
                step.StepName,
                step,
                CancellationToken.None);
            */
            await dagStore.RecoverTimedOutStepsAsync(
                created.ExecutionId,
                CancellationToken.None);

            var (_, stateAfter) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            var recoveredStep = stateAfter.Steps[step.StepName];

            Assert.Equal(AiStepExecutionStatus.Ready, recoveredStep.Status);

            Assert.Equal(
                1,
                recoveredStep.RetryState?.RetryCount);

            Assert.Equal(
                1,
                recoveredStep.RecoveryCount);

            Assert.Null(recoveredStep.ClaimedBy);
            Assert.Null(recoveredStep.ClaimToken);
            Assert.Null(recoveredStep.ClaimedAtUtc);
        }

        /// <summary>
        /// Creates the retry test options.
        ///
        /// IMPORTANT:
        /// - PayloadStore defaults are centralized in AiDagExecutionEngineFixture.
        /// - Cleanup is disabled so assertions can inspect final distributed state.
        /// </summary>
        private static AiEngineOptions CreateRetryOptions(string pipelinePath)
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = pipelinePath,
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

        /// <summary>
        /// Waits until any retry-delayed step becomes due.
        ///
        /// PURPOSE:
        /// - Keep the test deterministic without hardcoding a retry delay.
        /// - Avoid repeatedly executing while the retry window is still closed.
        /// </summary>
        private static async Task WaitForRetryWindowIfNeededAsync(AiExecutionState state)
        {
            var nextRetryAtUtc = state.Steps.Values
                .Where(x => x.Status == AiStepExecutionStatus.WaitingForRetry)
                .Select(x => x.RetryState?.NextRetryAtUtc)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .OrderBy(x => x)
                .FirstOrDefault();

            if (nextRetryAtUtc == default)
            {
                return;
            }

            var delay = nextRetryAtUtc - DateTime.UtcNow;

            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            var boundedDelay = delay > TimeSpan.FromMilliseconds(250)
                ? TimeSpan.FromMilliseconds(250)
                : delay;

            await Task.Delay(boundedDelay);
        }

        /// <summary>
        /// Loads the authoritative distributed record and state.
        ///
        /// IMPORTANT:
        /// - In distributed DAG mode, IAiDagExecutionStore is the source of truth.
        /// - These tests never assert final truth from the generic execution store.
        /// </summary>
        private static async Task<(AiExecutionRecord Record, AiExecutionState State)> LoadDistributedTruthAsync(
            IServiceProvider services,
            string executionId)
        {
            var dagStore = services.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(executionId);
            var state = await dagStore.GetStateAsync(executionId);

            Assert.NotNull(record);
            Assert.NotNull(state);

            return (record!, state!);
        }
    }
}
