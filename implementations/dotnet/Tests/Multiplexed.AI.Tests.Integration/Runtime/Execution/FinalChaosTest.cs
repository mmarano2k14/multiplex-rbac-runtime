using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Chaos and hardening tests for the distributed DAG execution runtime.
    ///
    /// PURPOSE:
    /// - Validate distributed execution correctness under concurrency
    /// - Validate retry and replay safety
    /// - Validate that the distributed DAG store remains the authoritative truth
    ///
    /// IMPORTANT:
    /// - These tests target the distributed DAG execution path
    /// - Assertions must read final truth from <see cref="IAiDagExecutionStore"/>
    ///   rather than the generic <see cref="IAiExecutionStore"/>
    /// - Replay tests require snapshot persistence to be enabled
    /// </summary>
    public sealed class FinalChaosTest
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "ai-tests";
        private const string CollectionName = "ai_execution_engine_snapshots_tests";

        // ============================================================
        // RETRY CHAOS TESTS
        // ============================================================

        /// <summary>
        /// Verifies that concurrent retry pressure does not corrupt retry budgeting
        /// or authoritative execution state.
        ///
        /// WHAT THIS TEST PROVES:
        /// - retry counters remain bounded by the configured retry budget
        /// - concurrent workers do not consume retry budget multiple times incorrectly
        /// - the authoritative record/state relationship remains coherent
        ///
        /// IMPORTANT:
        /// - This test validates retry safety, not terminal convergence
        /// - Under retry timing pressure, the execution may still be Running or Waiting
        ///   when the first worker wave completes
        /// </summary>
        [Fact]
        public async Task Concurrent_Retry_Should_Preserve_Retry_Budget_And_State_Correctness()
        {
            var options = CreateOptions();
            options.JsonPipelineDefinitionFilePath = "config\\dag-with-retry.json";

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-with-retry", "fail");

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            const int workerCount = 16;
            const int iterationsPerWorker = 10;

            // -----------------------------------------------------------------
            // Launch many concurrent workers repeatedly attempting to advance
            // the same execution while retry logic is active.
            // -----------------------------------------------------------------
            var tasks = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(async () =>
                {
                    for (var i = 0; i < iterationsPerWorker; i++)
                    {
                        try
                        {
                            await engine.ExecuteNextAsync(created.ExecutionId);
                        }
                        catch
                        {
                            // Worker-level contention and transient failures are tolerated.
                            // The purpose of this test is retry accounting and state safety.
                        }
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            var (finalRecord, finalState) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.NotNull(finalState.Steps);
            Assert.NotEmpty(finalState.Steps);

            foreach (var step in finalState.Steps.Values)
            {
                Assert.True(
                    step.RetryState?.RetryCount <= step.Retry?.MaxRetries,
                    $"Step '{step.StepName}' exceeded retry budget. RetryCount='{step.RetryState?.RetryCount}', MaxRetries='{step.Retry?.MaxRetries}'.");
            }

            var completedStepsFromState = finalState.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var completedStepsFromRecord = (finalRecord.CompletedSteps ?? new System.Collections.Generic.List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(completedStepsFromState, completedStepsFromRecord);

            // -----------------------------------------------------------------
            // If the execution reached a terminal state, terminal invariants must hold.
            // Otherwise, Running / Waiting remain acceptable for this safety-focused test.
            // -----------------------------------------------------------------
            if (finalRecord.IsTerminal)
            {
                Assert.True(
                    finalRecord.Status == AiExecutionStatus.Completed ||
                    finalRecord.Status == AiExecutionStatus.Failed ||
                    finalRecord.Status == AiExecutionStatus.Cancelled);

                Assert.True(string.IsNullOrEmpty(finalRecord.CurrentStep));

                Assert.DoesNotContain(
                    finalState.Steps.Values,
                    x => x.Status == AiStepExecutionStatus.Running ||
                         x.Status == AiStepExecutionStatus.Ready ||
                         x.Status == AiStepExecutionStatus.WaitingForRetry ||
                         x.Status == AiStepExecutionStatus.None);
            }
        }

        /// <summary>
        /// Verifies that a retry-capable execution eventually reaches a terminal state
        /// once enough bounded drain time is given for retry timing and scheduling to complete.
        ///
        /// WHAT THIS TEST PROVES:
        /// - retry scheduling eventually makes progress
        /// - the execution does not remain stuck forever in Running or Waiting
        /// - terminal convergence remains coherent after retry behavior
        ///
        /// IMPORTANT:
        /// - This test validates eventual convergence, not concurrent retry accounting
        /// - A bounded drain window is used because retry timing may delay completion
        /// </summary>
        [Fact]
        public async Task Retry_Execution_Should_Eventually_Reach_Terminal_State()
        {
            var options = CreateOptions();
            options.JsonPipelineDefinitionFilePath = "config\\dag-with-retry.json";

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-with-retry", "fail");

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var stateAfterCreate = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            DumpSteps("AFTER CREATE", stateAfterCreate);

            AiExecutionRecord? current = null;
            var deadlineUtc = DateTime.UtcNow.AddSeconds(20);

            // -----------------------------------------------------------------
            // Repeatedly advance the execution until terminal truth is reached
            // or until the bounded convergence window expires.
            // -----------------------------------------------------------------
            while (DateTime.UtcNow < deadlineUtc)
            {
                current = await engine.ExecuteNextAsync(created.ExecutionId);

                var stateAfterExecute = await host.ServiceProvider
                    .GetRequiredService<IAiDagExecutionStore>()
                    .GetStateAsync(created.ExecutionId);

                DumpSteps("AFTER EXECUTE", stateAfterExecute);

                if (current.IsTerminal)
                {
                    break;
                }

                await Task.Delay(100);
            }

            var (finalRecord, finalState) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.True(
                finalRecord.IsTerminal,
                $"Execution '{created.ExecutionId}' did not reach terminal state.{Environment.NewLine}{AiDagExecutionEngineFixture.DumpState(finalRecord, finalState)}");

            Assert.True(
                finalRecord.Status == AiExecutionStatus.Completed ||
                finalRecord.Status == AiExecutionStatus.Failed ||
                finalRecord.Status == AiExecutionStatus.Cancelled);

            Assert.NotNull(finalState.Steps);
            Assert.NotEmpty(finalState.Steps);

            Assert.DoesNotContain(
                finalState.Steps.Values,
                x => x.Status == AiStepExecutionStatus.Running ||
                     x.Status == AiStepExecutionStatus.Ready ||
                     x.Status == AiStepExecutionStatus.WaitingForRetry ||
                     x.Status == AiStepExecutionStatus.None);

            var completedStepsFromState = finalState.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var completedStepsFromRecord = (finalRecord.CompletedSteps ?? new System.Collections.Generic.List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(completedStepsFromState, completedStepsFromRecord);
            Assert.True(string.IsNullOrEmpty(finalRecord.CurrentStep));
        }

        // ============================================================
        // RECOVERY CHAOS TEST
        // ============================================================

        /// <summary>
        /// Verifies recovery safety under heavy concurrency.
        ///
        /// Guarantees:
        /// - no duplicate step execution
        /// - step completion truth remains consistent
        /// - record and state are aligned
        /// </summary>
        [Fact]
        public async Task Concurrent_Recovery_Should_Not_Corrupt_State()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-complex", "hello");

            const int workers = 12;
            const int iterations = 10;

            var tasks = Enumerable.Range(0, workers)
                .Select(_ => Task.Run(async () =>
                {
                    for (var i = 0; i < iterations; i++)
                    {
                        try
                        {
                            await engine.ExecuteNextAsync(created.ExecutionId);
                        }
                        catch
                        {
                        }
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            var (record, state) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            // Ensure no duplicate logical step entry
            var grouped = state.Steps.Values.GroupBy(x => x.StepName);

            foreach (var group in grouped)
            {
                Assert.Single(group);
            }

            // Ensure consistency record vs state
            var stateCompleted = state.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var recordCompleted = (record.CompletedSteps ?? new System.Collections.Generic.List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(stateCompleted, recordCompleted);

            if (record.IsTerminal)
            {
                Assert.True(
                    record.Status == AiExecutionStatus.Completed ||
                    record.Status == AiExecutionStatus.Failed ||
                    record.Status == AiExecutionStatus.Cancelled);

                Assert.DoesNotContain(
                    state.Steps.Values,
                    x => x.Status == AiStepExecutionStatus.Running ||
                         x.Status == AiStepExecutionStatus.Ready ||
                         x.Status == AiStepExecutionStatus.WaitingForRetry ||
                         x.Status == AiStepExecutionStatus.None);
            }
        }

        // ============================================================
        // REPLAY CHAOS TEST
        // ============================================================

        /// <summary>
        /// Verifies that replay remains idempotent even when many concurrent callers
        /// attempt to replay the same execution at the same time.
        ///
        /// WHAT THIS TEST PROVES:
        /// - concurrent replay requests do not corrupt runtime truth
        /// - replay remains safe when the execution already exists
        /// - at least one replay attempt completes without breaking the runtime store
        ///
        /// IMPORTANT:
        /// - This test validates replay idempotence and runtime safety
        /// - The critical guarantee is that replay under pressure does not corrupt
        ///   the authoritative distributed record/state pair
        /// </summary>
        [Fact]
        public async Task Concurrent_Replay_Should_Be_Idempotent_And_Safe()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;
            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();
            var snapshotStore = host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

            var created = await engine.CreateAsync("dag-complex", "hello");

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            // -----------------------------------------------------------------
            // First drive the execution until a replayable snapshot exists.
            // -----------------------------------------------------------------
            AiExecutionSnapshotDocument<ExecutionContextSnapshot>? snapshot = null;

            for (var i = 0; i < 60; i++)
            {
                await engine.ExecuteNextAsync(created.ExecutionId);

                snapshot = await snapshotStore.GetAsync(created.ExecutionId);

                if (snapshot is not null)
                {
                    break;
                }
            }

            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot!.Record);
            Assert.NotNull(snapshot.State);

            const int workerCount = 10;

            // -----------------------------------------------------------------
            // Launch many concurrent replay attempts against the same execution.
            // -----------------------------------------------------------------
            var tasks = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(async () =>
                {
                    try
                    {
                        return await replayService.ReplayAsync(
                            new AiExecutionReplayRequest
                            {
                                ExecutionId = created.ExecutionId,
                                Mode = AiExecutionReplayMode.ResumeIncomplete
                            });
                    }
                    catch
                    {
                        return null;
                    }
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            var (finalRecord, finalState) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.Equal(created.ExecutionId, finalRecord.ExecutionId);
            Assert.Equal(created.ExecutionId, finalState.ExecutionId);

            Assert.NotNull(finalState.Steps);
            Assert.NotEmpty(finalState.Steps);

            var completedStepsFromState = finalState.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var completedStepsFromRecord = (finalRecord.CompletedSteps ?? new System.Collections.Generic.List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(completedStepsFromState, completedStepsFromRecord);

            Assert.Contains(results, x => x is not null);

            Assert.Contains(results, x =>
                x is not null &&
                x.ReplayValid);

            Assert.DoesNotContain(results, x =>
                x is not null &&
                x.SnapshotFound &&
                !x.ReplayValid);

            if (finalRecord.IsTerminal)
            {
                Assert.True(
                    finalRecord.Status == AiExecutionStatus.Completed ||
                    finalRecord.Status == AiExecutionStatus.Failed ||
                    finalRecord.Status == AiExecutionStatus.Cancelled);
            }
        }

        // ============================================================
        // EXECUTE ALL CHAOS TEST
        // ============================================================

        /// <summary>
        /// Verifies that many concurrent ExecuteAllAsync calls against the same execution
        /// do not corrupt the authoritative runtime state, even if the execution does not
        /// reach a terminal state immediately.
        ///
        /// WHAT THIS TEST PROVES:
        /// - repeated high-level orchestration calls remain safe under concurrency
        /// - ExecuteAllAsync may legally stop before terminal convergence
        /// - record and authoritative step state remain coherent
        /// - no duplicate logical step entries are introduced
        ///
        /// IMPORTANT:
        /// - This test intentionally does NOT require terminal convergence
        /// - In this runtime, ExecuteAllAsync may stop while work is still in progress
        ///   or while distributed progress is still pending
        /// </summary>
        [Fact]
        public async Task Concurrent_ExecuteAllAsync_Should_Not_Corrupt_Authoritative_State_Under_Concurrency()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-complex", "hello");

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            const int workerCount = 10;

            var tasks = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(async () =>
                {
                    try
                    {
                        return await engine.ExecuteAllAsync(created.ExecutionId);
                    }
                    catch
                    {
                        return null;
                    }
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            var (finalRecord, finalState) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.Equal(created.ExecutionId, finalRecord.ExecutionId);
            Assert.Equal(created.ExecutionId, finalState.ExecutionId);

            Assert.NotNull(finalState.Steps);
            Assert.NotEmpty(finalState.Steps);

            // -----------------------------------------------------------------
            // The authoritative step collection must remain logically unique.
            // No duplicate logical step entries may appear under concurrency.
            // -----------------------------------------------------------------
            var groupedByStepName = finalState.Steps.Values
                .GroupBy(x => x.StepName, StringComparer.Ordinal)
                .ToList();

            foreach (var group in groupedByStepName)
            {
                Assert.Single(group);
            }

            // -----------------------------------------------------------------
            // Record projection must remain aligned with authoritative completed truth.
            // -----------------------------------------------------------------
            var completedStepsFromState = finalState.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var completedStepsFromRecord = (finalRecord.CompletedSteps ?? new System.Collections.Generic.List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(completedStepsFromState, completedStepsFromRecord);

            // -----------------------------------------------------------------
            // If the execution is terminal, terminal invariants must hold strongly.
            // If it is not terminal, Running / Waiting are still acceptable here.
            // -----------------------------------------------------------------
            if (finalRecord.IsTerminal)
            {
                Assert.True(
                    finalRecord.Status == AiExecutionStatus.Completed ||
                    finalRecord.Status == AiExecutionStatus.Failed ||
                    finalRecord.Status == AiExecutionStatus.Cancelled);

                Assert.True(string.IsNullOrEmpty(finalRecord.CurrentStep));

                Assert.DoesNotContain(
                    finalState.Steps.Values,
                    x => x.Status == AiStepExecutionStatus.Running ||
                         x.Status == AiStepExecutionStatus.Ready ||
                         x.Status == AiStepExecutionStatus.WaitingForRetry ||
                         x.Status == AiStepExecutionStatus.None);
            }

            Assert.Contains(results, x => x is not null);
        }

        // ============================================================
        // DISTRIBUTED TRUTH HELPERS
        // ============================================================

        /// <summary>
        /// Loads the authoritative distributed record and state from the DAG store.
        ///
        /// IMPORTANT:
        /// - In distributed DAG mode, <see cref="IAiDagExecutionStore"/> is the source of truth
        /// - Tests must not read final truth from <see cref="IAiExecutionStore"/> for distributed assertions
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

        // ============================================================
        // OPTIONS FACTORY
        // ============================================================

        /// <summary>
        /// Creates integration options suitable for distributed DAG chaos tests.
        ///
        /// IMPORTANT:
        /// - Snapshots are enabled so replay service registration is available
        /// - Cleanup remains disabled so tests can inspect final state
        /// </summary>
        private AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\dag-complex-10-steps.json",
                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = true,
                    Mongo = new AiExecutionSnapshotMongoOptions
                    {
                        Enabled = true,
                        CollectionName = CollectionName
                    }
                },
                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                },

                PayloadStore = new AiPayloadStoreOptions
                {
                    Enabled = true,
                    Provider = "mongo-redis",
                    RequireReplaySafePayloads = true,
                    MaxInlineSizeBytes = 512,

                    Mongo = new MongoAiPayloadStoreOptions
                    {
                        Enabled = true,
                        ConnectionString = ConnectionString,
                        DatabaseName = DatabaseName,
                        CollectionName = $"payloads_chaos_{Guid.NewGuid():N}"
                    },

                    RedisCache = new RedisAiPayloadCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:payload:chaos:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        MaxCacheablePayloadBytes = 1024 * 1024
                    },

                    StepIndexCache = new RedisAiStepPayloadIndexCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:step-index:chaos:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        RefreshTtlOnRead = true
                    }
                }
            };
        }

        /// <summary>
        /// Dumps current step state to console for targeted debugging.
        /// </summary>
        private static void DumpSteps(string label, AiExecutionState? state)
        {
            Console.WriteLine($"--- {label} ---");

            if (state is null)
            {
                Console.WriteLine("state = null");
                return;
            }

            foreach (var step in state.Steps.OrderBy(x => x.Key))
            {
                var s = step.Value;
                var dependsOn = s.DependsOn is null
                    ? "null"
                    : $"[{string.Join(", ", s.DependsOn)}]";

                Console.WriteLine(
                    $"Step={s.StepName}, Status={s.Status}, DependsOn={dependsOn}, RetryCount={s.RetryState?.RetryCount}");
            }
        }
    }
}