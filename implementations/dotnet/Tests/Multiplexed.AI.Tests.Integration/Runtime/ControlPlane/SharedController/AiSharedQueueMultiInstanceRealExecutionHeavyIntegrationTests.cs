using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.Admission;
using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Controller;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Dispatch;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Store;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Pump;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Queue;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Redis;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.Abstractions.AI.Observability.Tracing;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Observability.Ledger;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.ControlPlane.DI;
using Multiplexed.AI.Runtime.ControlPlane.Observability;
using Multiplexed.AI.Runtime.ControlPlane.SharedController;
using Multiplexed.AI.Runtime.ControlPlane.SharedController.Store;
using Multiplexed.AI.Runtime.ControlPlane.SharedQueue;
using Multiplexed.AI.Runtime.ControlPlane.ShareQueue.Redis;
using Multiplexed.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Runtime.Observability.Ledger.DI;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.MultipleInstance.Worker.Chaos;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.ControlPlane.SharedController
{
    /// <summary>
    /// Heavy multi-instance real execution integration tests for the shared queue control-plane.
    /// </summary>
    /// <remarks>
    /// This test validates the complete Kubernetes-like path without Kubernetes:
    ///
    /// SharedController
    /// -> QueueGlobally
    /// -> Redis shared run store
    /// -> Redis shared queue
    /// -> multiple runtime instances
    /// -> concurrent shared queue pumps
    /// -> real LocalAiSharedRunDispatcher per instance
    /// -> real local runtime queue per instance
    /// -> real distributed DAG workers per instance
    /// -> flaky steps
    /// -> retry
    /// -> throttling / provider concurrency
    /// -> retention compact + evict
    /// -> Mongo snapshots
    /// -> replay restore
    /// -> ledger / timeline / metrics.
    /// </remarks>
    [Collection("redis")]
    public sealed class AiSharedQueueMultiInstanceRealExecutionHeavyIntegrationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;

        private readonly string _runKeyPrefix =
            $"test:ai:shared-runs:real-heavy:{Guid.NewGuid():N}";

        private readonly string _queueKeyPrefix =
            $"test:ai:shared-queue:real-heavy:{Guid.NewGuid():N}";

        private IConnectionMultiplexer? _connection;

        public AiSharedQueueMultiInstanceRealExecutionHeavyIntegrationTests(
            ITestOutputHelper output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public async Task InitializeAsync()
        {
            _connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        }

        public async Task DisposeAsync()
        {
            if (_connection is null)
            {
                return;
            }

            var database = _connection.GetDatabase();

            var server = _connection.GetServer(
                _connection.GetEndPoints().First());

            var runKeys = server.Keys(
                    database: database.Database,
                    pattern: $"{_runKeyPrefix}*")
                .ToArray();

            var queueKeys = server.Keys(
                    database: database.Database,
                    pattern: $"{_queueKeyPrefix}*")
                .ToArray();

            var keys = runKeys
                .Concat(queueKeys)
                .ToArray();

            if (keys.Length > 0)
            {
                await database.KeyDeleteAsync(keys);
            }

            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        [RedisFact]
        public async Task SharedQueue_Should_Run_Real_MultiInstance_Executions_With_All_Features_Enabled()
        {
            await RunHeavyScenarioAsync(
                HeavyScenario.AllFeaturesHeavy());
        }

        private async Task RunHeavyScenarioAsync(
            HeavyScenario scenario)
        {
            ArgumentNullException.ThrowIfNull(scenario);

            var store = CreateRunStore();
            var queue = CreateQueue();

            var controller = new AiSharedRuntimeController(
                new QueueGloballyAdmissionController(
                    runtimeInstanceCount: scenario.RuntimeInstanceCount),
                store,
                queue,
                new NeverCalledSharedRunDispatcher(),
                new NoopAiRuntimeScaleOutRequestPublisher(),
                Options.Create(new AiSharedRuntimeControllerOptions()),
                new NoopAiControlPlaneObserver());

            var runtimeInstances = new List<RuntimeInstanceHarness>();

            try
            {
                for (var index = 1; index <= scenario.RuntimeInstanceCount; index++)
                {
                    var runtimeInstanceId = $"runtime-instance-{index}";

                    var harness = await CreateRuntimeInstanceHarnessAsync(
                        scenario,
                        runtimeInstanceId);

                    runtimeInstances.Add(harness);
                }

                foreach (var runtimeInstance in runtimeInstances)
                {
                    await runtimeInstance.BackgroundController.StartAsync();
                }

                var sharedRunIdPrefix =
                    $"shared-run-{scenario.ScenarioId}-";

                for (var runIndex = 0; runIndex < scenario.RunCount; runIndex++)
                {
                    var sharedRunId =
                        $"{sharedRunIdPrefix}{runIndex:D5}";

                    var submit = await controller.SubmitRunAsync(
                        new AiSharedRuntimeControllerRequest
                        {
                            Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                            RequestedSharedRunId = sharedRunId,
                            RunRequest = new AiRuntimePipelineRunRequest
                            {
                                PipelineName = scenario.PipelineName,
                                PipelineDefinition = scenario.PipelineDefinition,
                                Input = new
                                {
                                    candidateId = $"{scenario.CandidateIdPrefix}-{runIndex:D5}",
                                    source = scenario.Name,
                                    runIndex,
                                    runCount = scenario.RunCount,
                                    stepCount = scenario.StepCount,
                                    sharedQueue = true,
                                    multiInstance = true,
                                    realExecution = true,
                                    retry = true,
                                    retention = true,
                                    compaction = true,
                                    eviction = true,
                                    throttling = true,
                                    snapshot = true,
                                    replay = true,
                                    ledger = true,
                                    timeline = true,
                                    metrics = true
                                }
                            },
                            TenantId = "tenant-real-heavy",
                            PipelineKey = scenario.PipelineName,
                            CorrelationId = $"correlation-{sharedRunId}",
                            RequestedBy = "integration-test",
                            Source = "multi-instance-real-heavy-test",
                            Reason = "Queue globally for heavy multi-instance real execution.",
                            Metadata = new Dictionary<string, string>
                            {
                                ["scenario"] = scenario.Name,
                                ["scenario.id"] = scenario.ScenarioId,
                                ["run.index"] = runIndex.ToString(),
                                ["run.count"] = scenario.RunCount.ToString(),
                                ["step.count"] = scenario.StepCount.ToString(),
                                ["features"] = "flaky,retry,retention,compact,evict,throttling,snapshot,replay,ledger,timeline,metrics"
                            }
                        });

                    Assert.True(
                        submit.Success,
                        submit.FailureReason ?? $"Submit failed for shared run '{sharedRunId}'.");

                    Assert.NotNull(submit.Run);
                    Assert.Equal(
                        AiSharedRunStatus.QueuedGlobally,
                        submit.Run.Status);
                }

                var queuedRuns = (await store.ListAsync(
                        includeCancelled: true,
                        includeCompleted: true,
                        includeFailed: true))
                    .Where(run => run.SharedRunId.StartsWith(
                        sharedRunIdPrefix,
                        StringComparison.Ordinal))
                    .ToArray();

                Assert.Equal(scenario.RunCount, queuedRuns.Length);

                Assert.All(
                    queuedRuns,
                    run => Assert.Equal(AiSharedRunStatus.QueuedGlobally, run.Status));

                var queuedItems = (await queue.ListAsync(
                        includeTerminal: true))
                    .Where(item => item.SharedRunId.StartsWith(
                        sharedRunIdPrefix,
                        StringComparison.Ordinal))
                    .ToArray();

                Assert.Equal(scenario.RunCount, queuedItems.Length);

                Assert.All(
                    queuedItems,
                    item => Assert.Equal(AiSharedQueueItemStatus.Pending, item.Status));

                var pumpTasks = runtimeInstances
                    .Select(runtimeInstance =>
                        RunRuntimeInstancePumpUntilEmptyAsync(
                            runtimeInstance,
                            queue,
                            store,
                            scenario.MaxDispatchesPerPumpCycle))
                    .ToArray();

                var pumpResults = await Task.WhenAll(
                    pumpTasks);

                var successfulDispatchCount = pumpResults.Sum(
                    result => result.SuccessfulDispatchCount);

                var failedDispatchCount = pumpResults.Sum(
                    result => result.FailedDispatchCount);

                Assert.Equal(0, failedDispatchCount);
                Assert.Equal(scenario.RunCount, successfulDispatchCount);

                var finalRuns = await WaitForSharedRunsDispatchedAsync(
                    store,
                    sharedRunIdPrefix,
                    scenario.RunCount,
                    scenario.SharedDispatchTimeout);

                Assert.All(
                    finalRuns,
                    run =>
                    {
                        Assert.Equal(AiSharedRunStatus.Dispatched, run.Status);
                        Assert.False(string.IsNullOrWhiteSpace(run.AssignedRuntimeInstanceId));
                        Assert.False(string.IsNullOrWhiteSpace(run.LocalRunId));
                    });

                var duplicateDispatches = finalRuns
                    .GroupBy(run => run.SharedRunId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .ToArray();

                Assert.Empty(duplicateDispatches);

                var finalQueueItems = (await queue.ListAsync(
                        includeTerminal: true))
                    .Where(item => item.SharedRunId.StartsWith(
                        sharedRunIdPrefix,
                        StringComparison.Ordinal))
                    .ToArray();

                Assert.Equal(scenario.RunCount, finalQueueItems.Length);

                Assert.All(
                    finalQueueItems,
                    item => Assert.Equal(AiSharedQueueItemStatus.Dispatched, item.Status));

                var runsByInstance = finalRuns
                    .GroupBy(run => run.AssignedRuntimeInstanceId ?? string.Empty, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToArray(),
                        StringComparer.Ordinal);

                Assert.True(
                    runsByInstance.Count >= scenario.ExpectedMinimumParticipatingInstances,
                    $"Expected at least '{scenario.ExpectedMinimumParticipatingInstances}' runtime instances to receive runs, " +
                    $"but only '{runsByInstance.Count}' participated.");

                var completedExecutions = new List<CompletedExecutionInfo>();

                foreach (var run in finalRuns.OrderBy(run => run.SharedRunId, StringComparer.Ordinal))
                {
                    Assert.False(string.IsNullOrWhiteSpace(run.AssignedRuntimeInstanceId));
                    Assert.False(string.IsNullOrWhiteSpace(run.LocalRunId));

                    var runtimeInstance = runtimeInstances.Single(
                        instance => string.Equals(
                            instance.RuntimeInstanceId,
                            run.AssignedRuntimeInstanceId,
                            StringComparison.Ordinal));

                    var record = await WaitForRuntimeRunExecutionCompletedAsync(
                        runtimeInstance,
                        runtimeInstances,
                        run.LocalRunId!,
                        scenario.ExecutionTimeout);

                    var executionId = record.ExecutionId;

                    Assert.False(string.IsNullOrWhiteSpace(executionId));

                    var state = await runtimeInstance.DagStore.GetStateAsync(
                        executionId);

                    Assert.NotNull(state);

                    await runtimeInstance.Resolver.WarmAsync(
                        executionId,
                        state!,
                        CancellationToken.None);

                    await AssertRequiredStepsResolvedAsync(
                        scenario,
                        executionId,
                        state!,
                        runtimeInstance.Resolver);

                    AssertNoStaleClaims(
                        state!);

                    AssertRetentionDidNotBreakState(
                        scenario,
                        state!);

                    await AssertSnapshotExistsAsync(
                        runtimeInstance,
                        executionId,
                        scenario.SnapshotTimeout);

                    var ledgerEntries = await runtimeInstance.Ledger.GetByExecutionAsync(
                        executionId);

                    var timelineEvents = runtimeInstance.Timeline.Get(
                        executionId);

                    Assert.NotEmpty(ledgerEntries);
                    Assert.NotEmpty(timelineEvents);

                    AssertLedgerCategoryContainsAny(
                        ledgerEntries,
                        AiDecisionLedgerCategory.Execution,
                        "execution.");

                    AssertLedgerCategoryContainsAny(
                        ledgerEntries,
                        AiDecisionLedgerCategory.Run,
                        "run.");

                    AssertLedgerCategoryContainsAny(
                        ledgerEntries,
                        AiDecisionLedgerCategory.Claim,
                        "claim.");

                    AssertLedgerCategoryContainsAny(
                        ledgerEntries,
                        AiDecisionLedgerCategory.Step,
                        "step.");

                    if (scenario.ExpectedRetriedSteps.Count > 0)
                    {
                        AssertLedgerCategoryContainsAny(
                            ledgerEntries,
                            AiDecisionLedgerCategory.Retry,
                            "retry.");
                    }

                    if (scenario.RetentionPolicies.Count > 0)
                    {
                        AssertLedgerCategoryContainsAny(
                            ledgerEntries,
                            AiDecisionLedgerCategory.Retention,
                            "retention.");
                    }

                    completedExecutions.Add(
                        new CompletedExecutionInfo(
                            run.SharedRunId,
                            run.AssignedRuntimeInstanceId!,
                            run.LocalRunId!,
                            executionId,
                            runtimeInstance,
                            record));
                }

                Assert.Equal(
                    scenario.RunCount,
                    completedExecutions.Count);

                foreach (var runtimeInstance in runtimeInstances)
                {
                    AssertDistributedWorkerMetrics(
                        runtimeInstance,
                        scenario);
                }

                var replaySamples = SelectReplaySamples(
                    completedExecutions,
                    scenario.ReplaySampleCount);

                Assert.NotEmpty(replaySamples);

                foreach (var sample in replaySamples)
                {
                    await ValidateReplayAsync(
                        scenario,
                        sample);
                }

                PrintHeavySummary(
                    scenario,
                    completedExecutions,
                    replaySamples);
            }
            finally
            {
                foreach (var runtimeInstance in runtimeInstances)
                {
                    await runtimeInstance.DisposeAsync();
                }
            }
        }

        private async Task<RuntimeInstanceHarness> CreateRuntimeInstanceHarnessAsync(
            HeavyScenario scenario,
            string runtimeInstanceId)
        {
            ArgumentNullException.ThrowIfNull(scenario);
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);

            var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(
                    scenario,
                    runtimeInstanceId),
                configureServices: services =>
                {
                    var finalizedHook = new DistributedChaosRunFinalizedHook();

                    services.AddSingleton(finalizedHook);
                    services.AddSingleton<IAiRuntimePipelineRunLifecycleHook>(
                        finalizedHook);

                    services.AddInMemoryAiDecisionLedger();

                    services.AddAiControlPlane();

                    services.AddAiStepsFromAssemblies(
                        typeof(AiRuntimeAssemblyMarker).Assembly,
                        typeof(AiSharedQueueMultiInstanceRealExecutionHeavyIntegrationTests).Assembly,
                        typeof(DistributedChaosFlakyProviderStep).Assembly);
                });

            return new RuntimeInstanceHarness(
                runtimeInstanceId,
                host,
                host.ServiceProvider.GetRequiredService<IAiRuntimePipelineBackgroundController>(),
                host.ServiceProvider.GetRequiredService<IAiRuntimeQueueControlPlane>(),
                host.ServiceProvider.GetRequiredService<IAiSharedRunDispatcher>(),
                host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>(),
                host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>(),
                host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>(),
                host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>(),
                host.ServiceProvider.GetRequiredService<IAiDecisionLedger>(),
                host.ServiceProvider.GetRequiredService<IAiTraceTimeline>(),
                host.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>());
        }

        private async Task<AiSharedQueuePumpResult> RunRuntimeInstancePumpUntilEmptyAsync(
            RuntimeInstanceHarness runtimeInstance,
            IAiSharedQueue queue,
            IAiSharedRunStore store,
            int maxDispatchesPerPumpCycle)
        {
            ArgumentNullException.ThrowIfNull(runtimeInstance);
            ArgumentNullException.ThrowIfNull(queue);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDispatchesPerPumpCycle);

            var queueDispatcher = new AiSharedQueueDispatcher(
                queue,
                store,
                runtimeInstance.SharedRunDispatcher);

            var pump = new AiSharedQueuePump(
                queueDispatcher,
                Options.Create(new AiSharedQueuePumpOptions
                {
                    Enabled = true,
                    MaxDispatchesPerCycle = maxDispatchesPerPumpCycle,
                    DefaultClaimTtl = TimeSpan.FromSeconds(30),
                    StopCycleWhenNoItemAvailable = true,
                    StopCycleOnDispatchFailure = true,
                    WorkerId = $"{runtimeInstance.RuntimeInstanceId}-shared-queue-worker",
                    Source = "multi-instance-real-heavy-test"
                }));

            var startedAtUtc = DateTimeOffset.UtcNow;

            var attemptedDispatchCount = 0;
            var successfulDispatchCount = 0;
            var failedDispatchCount = 0;
            var stoppedBecauseNoItemAvailable = false;

            var cycles = 0;

            while (true)
            {
                cycles++;

                var result = await pump.PumpOnceAsync(
                    new AiSharedQueuePumpRequest
                    {
                        RuntimeInstanceId = runtimeInstance.RuntimeInstanceId,
                        WorkerId = $"{runtimeInstance.RuntimeInstanceId}-shared-queue-worker",
                        MaxDispatches = maxDispatchesPerPumpCycle,
                        ClaimTtl = TimeSpan.FromSeconds(30),
                        CorrelationId = $"correlation-{runtimeInstance.RuntimeInstanceId}-{cycles}",
                        RequestedBy = "integration-test",
                        Source = "multi-instance-real-heavy-test",
                        Reason = "Runtime instance consumes shared queue and dispatches to real local runtime.",
                        Metadata = new Dictionary<string, string>
                        {
                            ["runtime.instance.id"] = runtimeInstance.RuntimeInstanceId,
                            ["cycle"] = cycles.ToString(),
                            ["real.dispatch"] = "true"
                        }
                    });

                Assert.True(
                    result.Success,
                    result.FailureReason ?? $"Pump failed for runtime instance '{runtimeInstance.RuntimeInstanceId}'.");

                failedDispatchCount += result.FailedDispatchCount;
                attemptedDispatchCount += result.AttemptedDispatchCount;
                successfulDispatchCount += result.SuccessfulDispatchCount;
                stoppedBecauseNoItemAvailable = result.StoppedBecauseNoItemAvailable;

                if (result.StoppedBecauseNoItemAvailable)
                {
                    return new AiSharedQueuePumpResult
                    {
                        Success = true,
                        RuntimeInstanceId = runtimeInstance.RuntimeInstanceId,
                        AttemptedDispatchCount = attemptedDispatchCount,
                        SuccessfulDispatchCount = successfulDispatchCount,
                        FailedDispatchCount = failedDispatchCount,
                        StoppedBecauseNoItemAvailable = stoppedBecauseNoItemAvailable,
                        StartedAtUtc = startedAtUtc,
                        CompletedAtUtc = DateTimeOffset.UtcNow
                    };
                }

                Assert.True(
                    cycles <= 10_000,
                    $"Pump for runtime instance '{runtimeInstance.RuntimeInstanceId}' did not finish.");
            }
        }

        private static async Task<AiExecutionRecord> WaitForRuntimeRunExecutionCompletedAsync(
    RuntimeInstanceHarness expectedRuntimeInstance,
    IReadOnlyCollection<RuntimeInstanceHarness> allRuntimeInstances,
    string localRunId,
    TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(expectedRuntimeInstance);
            ArgumentNullException.ThrowIfNull(allRuntimeInstances);
            ArgumentException.ThrowIfNullOrWhiteSpace(localRunId);

            var deadline = DateTimeOffset.UtcNow.Add(timeout);

            string? executionId = null;
            string? lastRunStatus = null;
            AiExecutionStatus? lastExecutionStatus = null;
            string? runtimeInstanceWhereRunWasFound = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                foreach (var runtimeInstance in allRuntimeInstances)
                {
                    var result = await runtimeInstance.QueueControlPlane.GetRunStatusAsync(
                        new AiRuntimeQueueControlPlaneRequest
                        {
                            Operation = AiRuntimeQueueControlPlaneOperation.GetRunStatus,
                            RunId = localRunId,
                            RequestedBy = "integration-test",
                            Source = "multi-instance-real-heavy-test"
                        });

                    if (result.RunState is null)
                    {
                        continue;
                    }

                    runtimeInstanceWhereRunWasFound = runtimeInstance.RuntimeInstanceId;
                    lastRunStatus = result.RunState.Status;

                    if (!string.IsNullOrWhiteSpace(result.RunState.ExecutionId))
                    {
                        executionId = result.RunState.ExecutionId;
                    }

                    if (string.Equals(lastRunStatus, "failed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(lastRunStatus, "cancelled", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(lastRunStatus, "canceled", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Local run '{localRunId}' reached terminal failure status '{lastRunStatus}' " +
                            $"on runtime instance '{runtimeInstance.RuntimeInstanceId}'. " +
                            $"FailureReason='{result.RunState.FailureReason}'.");
                    }

                    if (!string.IsNullOrWhiteSpace(executionId))
                    {
                        var record = await runtimeInstance.DagStore.GetRecordAsync(
                            executionId);

                        if (record is not null)
                        {
                            lastExecutionStatus = record.Status;

                            if (record.Status == AiExecutionStatus.Completed)
                            {
                                return record;
                            }

                            if (record.Status == AiExecutionStatus.Failed ||
                                record.Status == AiExecutionStatus.Cancelled)
                            {
                                throw new InvalidOperationException(
                                    $"Execution '{executionId}' reached terminal status '{record.Status}' " +
                                    $"on runtime instance '{runtimeInstance.RuntimeInstanceId}'.");
                            }
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            throw new TimeoutException(
                $"Local run '{localRunId}' did not complete within '{timeout}'. " +
                $"ExpectedRuntimeInstance='{expectedRuntimeInstance.RuntimeInstanceId}', " +
                $"RuntimeInstanceWhereRunWasFound='{runtimeInstanceWhereRunWasFound ?? "none"}', " +
                $"LastRunStatus='{lastRunStatus ?? "none"}', " +
                $"ExecutionId='{executionId ?? "none"}', " +
                $"LastExecutionStatus='{lastExecutionStatus?.ToString() ?? "none"}'.");
        }

        private async Task<AiSharedRunRecord[]> WaitForSharedRunsDispatchedAsync(
            IAiSharedRunStore store,
            string sharedRunIdPrefix,
            int expectedCount,
            TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout);

            AiSharedRunRecord[] lastRuns = Array.Empty<AiSharedRunRecord>();

            while (DateTimeOffset.UtcNow < deadline)
            {
                lastRuns = (await store.ListAsync(
                        includeCancelled: true,
                        includeCompleted: true,
                        includeFailed: true))
                    .Where(run => run.SharedRunId.StartsWith(
                        sharedRunIdPrefix,
                        StringComparison.Ordinal))
                    .ToArray();

                if (lastRuns.Length == expectedCount &&
                    lastRuns.All(run => run.Status == AiSharedRunStatus.Dispatched))
                {
                    return lastRuns;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            throw new TimeoutException(
                $"Expected '{expectedCount}' shared runs to be dispatched within '{timeout}', " +
                $"but found '{lastRuns.Length}' matching runs. " +
                $"Statuses: {string.Join(", ", lastRuns.GroupBy(run => run.Status).Select(group => $"{group.Key}={group.Count()}"))}");
        }

        private async Task AssertSnapshotExistsAsync(
            RuntimeInstanceHarness runtimeInstance,
            string executionId,
            TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout);

            while (DateTimeOffset.UtcNow < deadline)
            {
                var snapshot = await runtimeInstance.SnapshotStore.GetAsync(
                    executionId);

                if (snapshot is not null)
                {
                    Assert.Equal(executionId, snapshot.ExecutionId);
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            throw new TimeoutException(
                $"Snapshot for execution '{executionId}' was not found within '{timeout}'.");
        }

        private async Task ValidateReplayAsync(
            HeavyScenario scenario,
            CompletedExecutionInfo execution)
        {
            var stateBeforeReplay = await execution.RuntimeInstance.DagStore.GetStateAsync(
                execution.ExecutionId);

            var recordBeforeReplay = await execution.RuntimeInstance.DagStore.GetRecordAsync(
                execution.ExecutionId);

            Assert.NotNull(stateBeforeReplay);
            Assert.NotNull(recordBeforeReplay);

            await execution.RuntimeInstance.Resolver.WarmAsync(
                execution.ExecutionId,
                stateBeforeReplay!,
                CancellationToken.None);

            var beforeFingerprint = await CreateReplayFingerprintAsync(
                scenario,
                execution.ExecutionId,
                recordBeforeReplay!,
                stateBeforeReplay!,
                execution.RuntimeInstance.Resolver);

            await CleanupDagExecutionAsync(
                execution.RuntimeInstance,
                execution.ExecutionId);

            Assert.Null(await execution.RuntimeInstance.DagStore.GetRecordAsync(execution.ExecutionId));
            Assert.Null(await execution.RuntimeInstance.DagStore.GetStateAsync(execution.ExecutionId));

            var replayResult = await execution.RuntimeInstance.ReplayService.ReplayAsync(
                new AiExecutionReplayRequest
                {
                    ExecutionId = execution.ExecutionId,
                    Mode = AiExecutionReplayMode.ResumeIncomplete,
                    IncludeLedgerEvents = true,
                    IncludeTimeline = true,
                    IncludeStepDetails = true,
                    ValidatePayloadReferences = true
                });

            Assert.NotNull(replayResult);
            Assert.True(replayResult.ReplayValid);
            Assert.True(replayResult.ExecutionFound);
            Assert.True(replayResult.SnapshotFound);
            Assert.True(replayResult.FingerprintFound);
            Assert.True(replayResult.FingerprintMatches);
            Assert.True(replayResult.DependencyGraphValid);
            Assert.True(replayResult.StepStateValid);
            Assert.True(replayResult.PayloadReferencesValid);

            var restoredRecord = await execution.RuntimeInstance.DagStore.GetRecordAsync(
                execution.ExecutionId);

            var restoredState = await execution.RuntimeInstance.DagStore.GetStateAsync(
                execution.ExecutionId);

            Assert.NotNull(restoredRecord);
            Assert.NotNull(restoredState);

            await execution.RuntimeInstance.Resolver.WarmAsync(
                execution.ExecutionId,
                restoredState!,
                CancellationToken.None);

            var afterFingerprint = await CreateReplayFingerprintAsync(
                scenario,
                execution.ExecutionId,
                restoredRecord!,
                restoredState!,
                execution.RuntimeInstance.Resolver);

            Assert.Equal(beforeFingerprint.Status, afterFingerprint.Status);
            Assert.Equal(beforeFingerprint.IsTerminal, afterFingerprint.IsTerminal);
            Assert.Equal(beforeFingerprint.CompletedSteps, afterFingerprint.CompletedSteps);
            Assert.Equal(beforeFingerprint.StepStatuses, afterFingerprint.StepStatuses);
            Assert.Equal(beforeFingerprint.RetryCounts, afterFingerprint.RetryCounts);
            Assert.Equal(beforeFingerprint.RequiredResolvedSteps, afterFingerprint.RequiredResolvedSteps);

            var ledgerAfterReplay = await execution.RuntimeInstance.Ledger.GetByExecutionAsync(
                execution.ExecutionId);

            AssertLedgerCategoryContainsAny(
                ledgerAfterReplay,
                AiDecisionLedgerCategory.Replay,
                "replay.");
        }

        private static async Task CleanupDagExecutionAsync(
            RuntimeInstanceHarness runtimeInstance,
            string executionId)
        {
            await runtimeInstance.DagStore.DeleteExecutionBundleAsync(
                executionId);
        }

        private void AssertDistributedWorkerMetrics(
            RuntimeInstanceHarness runtimeInstance,
            HeavyScenario scenario)
        {
            var workerCycles = runtimeInstance.Metrics.Worker.GetCyclesByRuntimeInstance();

            Assert.NotEmpty(workerCycles);

            _output.WriteLine("");
            _output.WriteLine($"WORKER METRICS FOR {runtimeInstance.RuntimeInstanceId}");
            _output.WriteLine("------------------------------------------------------------");

            foreach (var item in workerCycles.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                _output.WriteLine(
                    $"{item.Key,-80} | Cycles={item.Value}");
            }
        }

        private static async Task AssertRequiredStepsResolvedAsync(
            HeavyScenario scenario,
            string executionId,
            AiExecutionState state,
            IAiExecutionStepResolver resolver)
        {
            foreach (var stepName in scenario.RequiredResolvedSteps)
            {
                var step = await resolver.GetStepAsync(
                    executionId,
                    stepName,
                    state,
                    CancellationToken.None);

                Assert.NotNull(step);
                Assert.Equal(AiStepExecutionStatus.Completed, step!.Status);
            }

            foreach (var stepName in scenario.ExpectedRetriedSteps)
            {
                var step = await resolver.GetStepAsync(
                    executionId,
                    stepName,
                    state,
                    CancellationToken.None);

                Assert.NotNull(step);
                Assert.Equal(AiStepExecutionStatus.Completed, step!.Status);

                Assert.True(
                    step.RetryState?.RetryCount >= 1,
                    $"Expected step '{stepName}' to have RetryCount >= 1.");
            }
        }

        private static void AssertNoStaleClaims(
            AiExecutionState state)
        {
            Assert.All(
                state.Steps.Values,
                step =>
                {
                    Assert.Null(step.ClaimedBy);
                    Assert.Null(step.ClaimToken);
                    Assert.Null(step.ClaimedAtUtc);
                    Assert.Null(step.LeaseExpiresAtUtc);
                });
        }

        private static void AssertRetentionDidNotBreakState(
            HeavyScenario scenario,
            AiExecutionState state)
        {
            Assert.NotNull(state.Steps);

            Assert.True(
                state.Steps.Count <= scenario.StepCount);

            Assert.All(
                state.Steps.Values,
                step =>
                {
                    Assert.True(
                        step.Status == AiStepExecutionStatus.Completed ||
                        step.Status == AiStepExecutionStatus.Failed);
                });
        }

        private static void AssertLedgerCategoryContainsAny(
            IReadOnlyCollection<AiDecisionLedgerEntry> entries,
            AiDecisionLedgerCategory category,
            string eventPrefix)
        {
            Assert.Contains(
                entries,
                entry =>
                    entry.Category == category &&
                    entry.EventType.StartsWith(
                        eventPrefix,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static CompletedExecutionInfo[] SelectReplaySamples(
            IReadOnlyCollection<CompletedExecutionInfo> executions,
            int sampleCount)
        {
            var byInstance = executions
                .GroupBy(item => item.RuntimeInstanceId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            var remaining = executions
                .Where(item => byInstance.All(sample =>
                    !string.Equals(sample.ExecutionId, item.ExecutionId, StringComparison.Ordinal)))
                .Take(Math.Max(0, sampleCount - byInstance.Count));

            return byInstance
                .Concat(remaining)
                .Take(sampleCount)
                .ToArray();
        }

        private void PrintHeavySummary(
            HeavyScenario scenario,
            IReadOnlyCollection<CompletedExecutionInfo> executions,
            IReadOnlyCollection<CompletedExecutionInfo> replaySamples)
        {
            var distribution = executions
                .GroupBy(item => item.RuntimeInstanceId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);

            _output.WriteLine("");
            _output.WriteLine("============================================================");
            _output.WriteLine("MULTI-INSTANCE REAL HEAVY EXECUTION TEST");
            _output.WriteLine("============================================================");
            _output.WriteLine($"Scenario:             {scenario.Name}");
            _output.WriteLine($"ScenarioId:           {scenario.ScenarioId}");
            _output.WriteLine($"Runs:                 {scenario.RunCount}");
            _output.WriteLine($"StepsPerRun:          {scenario.StepCount}");
            _output.WriteLine($"NominalSteps:         {scenario.RunCount * scenario.StepCount}");
            _output.WriteLine($"RuntimeInstances:     {scenario.RuntimeInstanceCount}");
            _output.WriteLine($"WorkersPerInstance:   {scenario.WorkerCount}");
            _output.WriteLine($"ReplaySamples:        {replaySamples.Count}");
            _output.WriteLine("");
            _output.WriteLine("DISTRIBUTION BY RUNTIME INSTANCE");
            _output.WriteLine("------------------------------------------------------------");

            foreach (var item in distribution.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                _output.WriteLine(
                    $"{item.Key,-35} | Runs={item.Value}");
            }

            _output.WriteLine("");
            _output.WriteLine("REPLAY SAMPLES");
            _output.WriteLine("------------------------------------------------------------");

            foreach (var sample in replaySamples.OrderBy(item => item.RuntimeInstanceId, StringComparer.Ordinal))
            {
                _output.WriteLine(
                    $"{sample.RuntimeInstanceId,-35} | SharedRunId={sample.SharedRunId} | ExecutionId={sample.ExecutionId}");
            }
        }

        private RedisAiSharedRunStore CreateRunStore()
        {
            if (_connection is null)
            {
                throw new InvalidOperationException("Redis connection was not initialized.");
            }

            return new RedisAiSharedRunStore(
                _connection,
                Options.Create(new RedisAiSharedRunStoreOptions
                {
                    KeyPrefix = _runKeyPrefix,
                    ListScanLimit = 20_000
                }));
        }

        private RedisAiSharedQueue CreateQueue()
        {
            if (_connection is null)
            {
                throw new InvalidOperationException("Redis connection was not initialized.");
            }

            return new RedisAiSharedQueue(
                _connection,
                Options.Create(new RedisAiSharedQueueOptions
                {
                    KeyPrefix = _queueKeyPrefix,
                    ListScanLimit = 20_000
                }));
        }

        private static AiEngineOptions CreateOptions(
            HeavyScenario scenario,
            string runtimeInstanceId)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "Runtime",
                RuntimeInstanceWorker = new AiRuntimeInstanceWorkerOptions
                {
                    MaxStepsPerCycle = scenario.MaxStepsPerCycle,
                    IdleDelay = scenario.WorkerIdleDelay,
                    MaxCycles = scenario.MaxWorkerCycles,
                    IgnoreConcurrencyConflicts = true
                },
                PipelineBackgroundController = new AiRuntimePipelineBackgroundControllerOptions
                {
                    MaxConcurrentRuns = scenario.MaxConcurrentRunsPerInstance,
                    QueueCapacity = scenario.QueueCapacityPerInstance,
                    RejectEnqueueWhenStopped = false,
                    StopOnFirstFailure = false,
                    Distributed = new AiRuntimeDistributedExecutionOptions
                    {
                        Enabled = true,
                        WorkerCount = scenario.WorkerCount,
                        StopOnFirstTerminal = true,
                        TerminalObservationTimeout = TimeSpan.FromSeconds(60)
                    }
                }
            };

            options.Observability.EnableTracing = true;
            options.Observability.EnableInMemoryRecording = true;
            options.Observability.EnableMetrics = true;

            options.Observability.DecisionLedger.WriteMode = AiDecisionLedgerWriteMode.BestEffort;
            options.Observability.DecisionLedger.StorageMode = AiDecisionLedgerStorageMode.InMemory;

            options.Snapshots.Enabled = true;
            options.Snapshots.Mongo.Enabled = true;
            options.Snapshots.Mongo.ConnectionString = "mongodb://localhost:27017";
            options.Snapshots.Mongo.DatabaseName = "multiplexed_ai_tests";
            options.Snapshots.Mongo.CollectionName =
                $"execution_snapshots_real_heavy_{runtimeInstanceId}_{Guid.NewGuid():N}";

            options.Cleanup.AutoCleanupOnCompleted = false;
            options.Cleanup.AutoCleanupOnFailed = false;
            options.Cleanup.SuppressSnapshotIfExist = true;
            options.Cleanup.SuppressCleanupExceptions = true;

            return options;
        }

        private static AiPipelineDefinition CreatePipelineDefinition(
            HeavyScenario scenario)
        {
            var steps = new List<AiPipelineStepDefinition>
            {
                new()
                {
                    Name = "chaos-step-001",
                    StepKey = "hello-world",
                    Order = 1,
                    Config = CreateStepConfig(
                        scenario,
                        index: 1,
                        isFlaky: false)
                }
            };

            for (var index = 2; index < scenario.StepCount; index++)
            {
                var isFlaky =
                    index % scenario.FlakyStepInterval == 0;

                steps.Add(
                    new AiPipelineStepDefinition
                    {
                        Name = $"chaos-step-{index:000}",
                        StepKey = isFlaky
                            ? "distributed.chaos.flaky-provider"
                            : "hello-world",
                        Order = index,
                        DependsOn = new[] { "chaos-step-001" },
                        Config = CreateStepConfig(
                            scenario,
                            index,
                            isFlaky)
                    });
            }

            steps.Add(
                new AiPipelineStepDefinition
                {
                    Name = "final-join-step",
                    StepKey = "hello-world",
                    Order = scenario.StepCount,
                    DependsOn = Enumerable.Range(2, scenario.StepCount - 2)
                        .Select(index => $"chaos-step-{index:000}")
                        .ToArray(),
                    Config = new Dictionary<string, object?>
                    {
                        ["provider"] = "openai",
                        ["model"] = "gpt-4.1",
                        ["operation"] = "llm.compose",
                        ["delayMs"] = 5
                    }
                });

            return new AiPipelineDefinition
            {
                Name = scenario.PipelineName,
                Version = "1.0.0",
                ExecutionMode = AiExecutionMode.Dag,
                Config = new Dictionary<string, object?>
                {
                    ["concurrency"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["maxDegreeOfParallelism"] = scenario.MaxDegreeOfParallelism,
                        ["maxProviderConcurrency"] = scenario.MaxProviderConcurrency,
                        ["leaseSeconds"] = 60,
                        ["defaultRetryAfterMs"] = 10,
                        ["jitter"] = false
                    },
                    ["retention"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["policies"] = scenario.RetentionPolicies.ToArray(),
                        ["archiveReason"] = scenario.RetentionArchiveReason,
                        ["trigger"] = new Dictionary<string, object?>
                        {
                            ["enabled"] = true,
                            ["maxStepsInState"] = scenario.MaxCompletedStepsInState,
                            ["maxCompletedStepsInState"] = scenario.MaxCompletedStepsInState,
                            ["maxInlinePayloadBytes"] = scenario.MaxInlinePayloadBytes
                        }
                    }
                },
                Steps = steps
            };
        }

        private static Dictionary<string, object?> CreateStepConfig(
            HeavyScenario scenario,
            int index,
            bool isFlaky)
        {
            var config = new Dictionary<string, object?>
            {
                ["provider"] = "openai",
                ["model"] = "gpt-4.1",
                ["operation"] = "llm.chat",
                ["delayMs"] = isFlaky ? 10 : 1
            };

            if (isFlaky)
            {
                config["attemptKey"] =
                    $"{scenario.PipelineName}:chaos-step-{index:000}";

                config["retry"] = new Dictionary<string, object?>
                {
                    ["maxRetries"] = 2,
                    ["strategy"] = "Fixed",
                    ["baseDelayMs"] = 15,
                    ["maxDelayMs"] = 15,
                    ["jitter"] = false
                };
            }

            return config;
        }

        private static async Task<ReplayFingerprint> CreateReplayFingerprintAsync(
            HeavyScenario scenario,
            string executionId,
            AiExecutionRecord record,
            AiExecutionState state,
            IAiExecutionStepResolver resolver)
        {
            var selectedSteps = scenario.PipelineDefinition.Steps
                .Where(step =>
                    !string.IsNullOrWhiteSpace(step.Name) &&
                    scenario.FingerprintStepNames.Contains(
                        step.Name,
                        StringComparer.Ordinal));

            var stepStatuses = new SortedDictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var step in selectedSteps)
            {
                var status = await resolver.GetStepStatusAsync(
                    executionId,
                    step.Name!,
                    state,
                    CancellationToken.None);

                Assert.NotNull(status);

                stepStatuses[step.Name!] = status!.Status.ToString();
            }

            var retryCounts = new SortedDictionary<string, int>(
                StringComparer.Ordinal);

            foreach (var stepName in scenario.ExpectedRetriedSteps)
            {
                var step = await resolver.GetStepAsync(
                    executionId,
                    stepName,
                    state,
                    CancellationToken.None);

                Assert.NotNull(step);

                retryCounts[stepName] =
                    step!.RetryState?.RetryCount ?? 0;
            }

            var required = new SortedDictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var stepName in scenario.RequiredResolvedSteps)
            {
                var step = await resolver.GetStepAsync(
                    executionId,
                    stepName,
                    state,
                    CancellationToken.None);

                Assert.NotNull(step);

                required[stepName] = step!.Status.ToString();
            }

            return new ReplayFingerprint
            {
                Status = record.Status.ToString(),
                IsTerminal = record.IsTerminal,
                CompletedSteps = record.CompletedSteps
                    .OrderBy(step => step, StringComparer.Ordinal)
                    .ToArray(),
                StepStatuses = stepStatuses,
                RetryCounts = retryCounts,
                RequiredResolvedSteps = required
            };
        }

        private sealed class QueueGloballyAdmissionController : IAiRunAdmissionController
        {
            private readonly int _runtimeInstanceCount;

            public QueueGloballyAdmissionController(
                int runtimeInstanceCount)
            {
                _runtimeInstanceCount = runtimeInstanceCount;
            }

            public Task<AiRunAdmissionDecision> AdmitAsync(
                AiRunAdmissionRequest request,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.QueueGlobally,
                    Reason = "Heavy multi-instance test queues globally.",
                    VisibleInstanceCount = _runtimeInstanceCount,
                    AvailableInstanceCount = 0,
                    CurrentInstanceCount = _runtimeInstanceCount,
                    MaxInstanceCount = _runtimeInstanceCount,
                    Metadata = request.Metadata
                });
            }
        }

        private sealed class NeverCalledSharedRunDispatcher : IAiSharedRunDispatcher
        {
            public Task<AiSharedRunDispatchResult> DispatchAsync(
                AiSharedRunDispatchRequest request,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "The shared runtime controller should not directly dispatch runs when admission queues globally.");
            }
        }

        private sealed class RuntimeInstanceHarness : IAsyncDisposable
        {
            public RuntimeInstanceHarness(
                string runtimeInstanceId,
                AiDagExecutionEngineTestHost host,
                IAiRuntimePipelineBackgroundController backgroundController,
                IAiRuntimeQueueControlPlane queueControlPlane,
                IAiSharedRunDispatcher sharedRunDispatcher,
                IAiDagExecutionStore dagStore,
                IAiExecutionStepResolver resolver,
                IAiExecutionReplayService replayService,
                IAiExecutionSnapshotStore<ExecutionContextSnapshot> snapshotStore,
                IAiDecisionLedger ledger,
                IAiTraceTimeline timeline,
                IAiRuntimeMetrics metrics)
            {
                RuntimeInstanceId = runtimeInstanceId;
                Host = host;
                BackgroundController = backgroundController;
                QueueControlPlane = queueControlPlane;
                SharedRunDispatcher = sharedRunDispatcher;
                DagStore = dagStore;
                Resolver = resolver;
                ReplayService = replayService;
                SnapshotStore = snapshotStore;
                Ledger = ledger;
                Timeline = timeline;
                Metrics = metrics;
            }

            public string RuntimeInstanceId { get; }

            public AiDagExecutionEngineTestHost Host { get; }

            public IAiRuntimePipelineBackgroundController BackgroundController { get; }

            public IAiRuntimeQueueControlPlane QueueControlPlane { get; }

            public IAiSharedRunDispatcher SharedRunDispatcher { get; }

            public IAiDagExecutionStore DagStore { get; }

            public IAiExecutionStepResolver Resolver { get; }

            public IAiExecutionReplayService ReplayService { get; }

            public IAiExecutionSnapshotStore<ExecutionContextSnapshot> SnapshotStore { get; }

            public IAiDecisionLedger Ledger { get; }

            public IAiTraceTimeline Timeline { get; }

            public IAiRuntimeMetrics Metrics { get; }

            public async ValueTask DisposeAsync()
            {
                await BackgroundController.StopAsync();
                await Host.DisposeAsync();
            }
        }

        private sealed record CompletedExecutionInfo(
            string SharedRunId,
            string RuntimeInstanceId,
            string LocalRunId,
            string ExecutionId,
            RuntimeInstanceHarness RuntimeInstance,
            AiExecutionRecord Record);

        private sealed class HeavyScenario
        {
            public required string Name { get; init; }

            public required string ScenarioId { get; init; }

            public required string PipelineName { get; init; }

            public required string CandidateIdPrefix { get; init; }

            public required string RetentionArchiveReason { get; init; }

            public AiPipelineDefinition PipelineDefinition { get; set; } = null!;

            public int RuntimeInstanceCount { get; init; }

            public int ExpectedMinimumParticipatingInstances { get; init; }

            public int RunCount { get; init; }

            public int StepCount { get; init; }

            public int WorkerCount { get; init; }

            public int MaxConcurrentRunsPerInstance { get; init; }

            public int QueueCapacityPerInstance { get; init; }

            public int MaxDispatchesPerPumpCycle { get; init; }

            public int MaxStepsPerCycle { get; init; }

            public int MaxWorkerCycles { get; init; }

            public int MaxDegreeOfParallelism { get; init; }

            public int MaxProviderConcurrency { get; init; }

            public int MaxCompletedStepsInState { get; init; }

            public int MaxInlinePayloadBytes { get; init; }

            public int FlakyStepInterval { get; init; }

            public int ReplaySampleCount { get; init; }

            public TimeSpan WorkerIdleDelay { get; init; }

            public TimeSpan SharedDispatchTimeout { get; init; }

            public TimeSpan ExecutionTimeout { get; init; }

            public TimeSpan SnapshotTimeout { get; init; }

            public IReadOnlyCollection<string> RequiredResolvedSteps { get; init; } =
                Array.Empty<string>();

            public IReadOnlyCollection<string> ExpectedRetriedSteps { get; init; } =
                Array.Empty<string>();

            public IReadOnlyCollection<string> FingerprintStepNames { get; init; } =
                Array.Empty<string>();

            public IReadOnlyCollection<string> RetentionPolicies { get; init; } =
                Array.Empty<string>();

            public static HeavyScenario AllFeaturesHeavy()
            {
                var scenarioId = Guid.NewGuid().ToString("N");

                var pipelineName =
                    $"shared-queue-real-heavy-{scenarioId}";

                var requiredSteps = new[]
                {
                    "chaos-step-001",
                    "chaos-step-009",
                    "chaos-step-018",
                    "chaos-step-045",
                    "chaos-step-090",
                    "final-join-step"
                };

                var retriedSteps = Enumerable.Range(2, 98)
                    .Where(index => index % 9 == 0)
                    .Select(index => $"chaos-step-{index:000}")
                    .ToArray();

                var scenario = new HeavyScenario
                {
                    Name = "shared-queue-real-heavy-all-features",
                    ScenarioId = scenarioId,
                    PipelineName = pipelineName,
                    CandidateIdPrefix = "candidate-shared-queue-real-heavy",
                    RetentionArchiveReason = "shared-queue-real-heavy-retention",
                    RuntimeInstanceCount = 3,
                    ExpectedMinimumParticipatingInstances = 2,
                    RunCount = 9,
                    StepCount = 100,
                    WorkerCount = 10,
                    MaxConcurrentRunsPerInstance = 3,
                    QueueCapacityPerInstance = 64,
                    MaxDispatchesPerPumpCycle = 2,
                    MaxStepsPerCycle = 1,
                    MaxWorkerCycles = 5000,
                    MaxDegreeOfParallelism = 12,
                    MaxProviderConcurrency = 3,
                    MaxCompletedStepsInState = 15,
                    MaxInlinePayloadBytes = 1,
                    FlakyStepInterval = 9,
                    ReplaySampleCount = 3,
                    WorkerIdleDelay = TimeSpan.FromMilliseconds(1),
                    SharedDispatchTimeout = TimeSpan.FromSeconds(60),
                    ExecutionTimeout = TimeSpan.FromMinutes(5),
                    SnapshotTimeout = TimeSpan.FromMinutes(2),
                    RetentionPolicies = new[]
                    {
                        "retention.compact.terminal",
                        "retention.evict.terminal"
                    },
                    RequiredResolvedSteps = requiredSteps,
                    ExpectedRetriedSteps = retriedSteps,
                    FingerprintStepNames = requiredSteps
                        .Concat(retriedSteps)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                };

                scenario.PipelineDefinition = CreatePipelineDefinition(
                    scenario);

                return scenario;
            }
        }

        private sealed class ReplayFingerprint
        {
            public required string Status { get; init; }

            public required bool IsTerminal { get; init; }

            public required IReadOnlyList<string> CompletedSteps { get; init; }

            public required IReadOnlyDictionary<string, string> StepStatuses { get; init; }

            public required IReadOnlyDictionary<string, int> RetryCounts { get; init; }

            public required IReadOnlyDictionary<string, string> RequiredResolvedSteps { get; init; }
        }
    }
}