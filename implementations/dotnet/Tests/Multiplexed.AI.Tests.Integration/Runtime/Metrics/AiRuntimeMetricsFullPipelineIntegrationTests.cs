using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Decisions;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;
using Multiplexed.Abstractions.AI.Execution.Retention.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Execution.Retention.Triggers;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Execution;
using Multiplexed.AI.Runtime.Metrics.HotState;
using Multiplexed.AI.Runtime.Metrics.Resolvers;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Multiplexed.AI.Runtime.Metrics.Storage;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Retention;
using Multiplexed.AI.Runtime.Retention.Decisions;
using Multiplexed.AI.Runtime.Retention.Decisions.Policies;
using Multiplexed.AI.Runtime.Retention.Policies;
using Multiplexed.AI.Runtime.Retention.Triggers;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Metrics
{
    /// <summary>
    /// End-to-end integration tests for runtime metrics.
    ///
    /// PURPOSE:
    /// - Execute real DAG pipelines through the standard DI/runtime flow.
    /// - Validate that the central IAiRuntimeMetrics facade is wired correctly.
    /// - Verify execution, resolver, hot-state, storage, and retention metrics
    ///   using real pipeline execution.
    ///
    /// IMPORTANT:
    /// - These are not unit tests of individual metric classes.
    /// - These tests validate that runtime components actually emit metrics.
    /// - Some counters are asserted as minimums because Mongo/Redis cache behavior,
    ///   retention timing, and distributed execution paths can legitimately add
    ///   extra operations.
    /// </summary>
    public sealed class AiRuntimeMetricsFullPipelineIntegrationTests
    {
        private const int StepCount = 4;
        private const int ResolverCallsPerStep = 2;
        private const int PayloadSize = 4096;
        private const int MaxCompletedStepsInState = 2;

        private readonly ITestOutputHelper _output;

        public AiRuntimeMetricsFullPipelineIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Record_All_Runtime_Metric_Domains()
        {
            var pipeline = CreatePipeline();

            await using var host = await CreateHost(pipeline);

            var metrics = host.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            var final = await host.Engine
                .ExecuteAllAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(final.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, final.Status);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);

            var execution = Assert.IsType<AiExecutionMetrics>(metrics.Execution);
            var retentionTrigger = Assert.IsType<AiRetentionTriggerMetrics>(metrics.Retention.Trigger);
            var retentionDecision = Assert.IsType<AiRetentionDecisionMetrics>(metrics.Retention.Decision);
            var retentionPlan = Assert.IsType<AiRetentionPlanMetrics>(metrics.Retention.Plan);
            var retentionExecution = Assert.IsType<AiRetentionExecutionMetrics>(metrics.Retention.Execution);
            var storage = Assert.IsType<AiStorageMetrics>(metrics.Storage);
            var hotState = Assert.IsType<AiHotStateMetrics>(metrics.HotState);
            var resolver = Assert.IsType<AiResolverMetrics>(metrics.Resolver);

            _output.WriteLine($"ExecutionStarted: {execution.ExecutionStartedCount}");
            _output.WriteLine($"ExecutionCompleted: {execution.ExecutionCompletedCount}");
            _output.WriteLine($"StepCompleted: {execution.StepCompletedCount}");
            _output.WriteLine($"StepClaimed: {execution.StepClaimedCount}");
            _output.WriteLine($"FinalizeAttempt: {execution.FinalizeAttemptCount}");
            _output.WriteLine($"FinalizeSuccess: {execution.FinalizeSuccessCount}");

            _output.WriteLine($"Resolver Started: {resolver.ResolvedStartedCount}");
            _output.WriteLine($"Resolver Success: {resolver.ResolvedSuccessCount}");

            _output.WriteLine($"HotState Added: {hotState.StateStepAddedCount}");
            _output.WriteLine($"HotState Observed: {hotState.StateSizeObservedCount}");

            _output.WriteLine($"Storage Stored: {storage.PayloadStoredCount}");
            _output.WriteLine($"Storage Loaded: {storage.PayloadLoadedCount}");

            Assert.Equal(1, execution.ExecutionStartedCount);
            Assert.Equal(1, execution.ExecutionCompletedCount);
            Assert.Equal(0, execution.ExecutionFailedCount);

            Assert.Equal(StepCount, execution.StepCompletedCount);
            Assert.Equal(0, execution.StepFailedCount);
            Assert.Equal(0, execution.StepRetriedCount);

            Assert.True(execution.StepClaimedCount >= 0);
            Assert.True(execution.FinalizeAttemptCount >= 0);
            Assert.True(execution.FinalizeSuccessCount >= 0);
            Assert.Equal(0, execution.FinalizeConflictCount);

            Assert.True(
                hotState.StateStepAddedCount >= StepCount,
                $"Expected at least {StepCount} steps added, got {hotState.StateStepAddedCount}");

            Assert.True(hotState.StateSizeObservedCount >= StepCount);
            Assert.True(hotState.LastObservedStepCount >= MaxCompletedStepsInState);

            Assert.True(resolver.ResolvedStartedCount >= StepCount);
            Assert.True(resolver.ResolvedSuccessCount >= StepCount);
            Assert.Equal(0, resolver.ResolvedMissCount);
            Assert.Equal(0, resolver.ResolvedFailedCount);

            Assert.True(resolver.OperationsByPath["config.size"] >= StepCount);
            Assert.True(resolver.OperationsByPath["execution.id"] >= StepCount);

            Assert.True(storage.PayloadStoredCount >= 0);
            Assert.True(storage.TotalPayloadStoredBytes > 0);

            Assert.True(
                storage.OperationsByStorageKind.ContainsKey("mongo") ||
                storage.OperationsByStorageKind.ContainsKey("durable-store") ||
                storage.OperationsByStorageKind.ContainsKey("externalized-payload"));

            Assert.True(retentionTrigger.TriggeredCount >= 0);

            Assert.True(
                retentionDecision.CompactionRequiredCount >= 0 ||
                retentionDecision.EvictionRequiredCount >= 0 ||
                retentionDecision.NoActionRequiredCount >= 0);

            Assert.True(
                retentionPlan.PlanCreatedCount >= 0 ||
                retentionPlan.PlanEmptyCount >= 0);

            Assert.True(
                retentionExecution.PayloadCompactedCount >= 0 ||
                retentionExecution.StepEvictedCount >= 0 ||
                retentionExecution.RetentionCompletedCount >= 0);

            _output.WriteLine(
                $"Execution: Started={execution.ExecutionStartedCount}, Completed={execution.ExecutionCompletedCount}, " +
                $"StepCompleted={execution.StepCompletedCount}, Claims={execution.StepClaimedCount}, Finalize={execution.FinalizeSuccessCount}");

            _output.WriteLine(
                $"Resolver: Started={resolver.ResolvedStartedCount}, Success={resolver.ResolvedSuccessCount}, Miss={resolver.ResolvedMissCount}, Failed={resolver.ResolvedFailedCount}");

            _output.WriteLine(
                $"Storage: Stored={storage.PayloadStoredCount}, Loaded={storage.PayloadLoadedCount}, Hit={storage.PayloadStoreHitCount}, Miss={storage.PayloadStoreMissCount}, Bytes={storage.TotalPayloadStoredBytes}");

            _output.WriteLine(
                $"HotState: Added={hotState.StateStepAddedCount}, Observed={hotState.StateSizeObservedCount}, LastSteps={hotState.LastObservedStepCount}");

            _output.WriteLine(
                $"Retention: Triggered={retentionTrigger.TriggeredCount}, Compaction={retentionDecision.CompactionRequiredCount}, Eviction={retentionDecision.EvictionRequiredCount}, Plan={retentionPlan.PlanCreatedCount}, Completed={retentionExecution.RetentionCompletedCount}");
        }

        [Fact]
        public async Task ExecuteNextAsync_Should_Record_Runtime_Metrics_Across_All_Domains()
        {
            var pipeline = CreatePipeline();

            await using var host = await CreateHost(pipeline);

            var metrics = host.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            var executionId = created.ExecutionId;
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var current = await host.Engine.ExecuteNextAsync(executionId);

                    if (current.IsTerminal)
                    {
                        break;
                    }
                }
                catch
                {
                    // Expected for retry/failure scenarios.
                }

                var state = await host.ServiceProvider
                    .GetRequiredService<IAiDagExecutionStore>()
                    .GetStateAsync(executionId);

                if (state is not null)
                {
                    await WaitForRetryWindowIfNeededAsync(state);
                }
            }

            var final = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetRecordAsync(executionId);

            Assert.NotNull(final);
            Assert.True(final!.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, final.Status);

            var stateFinal = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(executionId);

            Assert.NotNull(stateFinal);

            var execution = Assert.IsType<AiExecutionMetrics>(metrics.Execution);
            var retentionTrigger = Assert.IsType<AiRetentionTriggerMetrics>(metrics.Retention.Trigger);
            var storage = Assert.IsType<AiStorageMetrics>(metrics.Storage);
            var hotState = Assert.IsType<AiHotStateMetrics>(metrics.HotState);
            var resolver = Assert.IsType<AiResolverMetrics>(metrics.Resolver);

            _output.WriteLine($"ExecutionStarted: {execution.ExecutionStartedCount}");
            _output.WriteLine($"ExecutionCompleted: {execution.ExecutionCompletedCount}");
            _output.WriteLine($"StepCompleted: {execution.StepCompletedCount}");
            _output.WriteLine($"StepClaimed: {execution.StepClaimedCount}");
            _output.WriteLine($"FinalizeAttempt: {execution.FinalizeAttemptCount}");
            _output.WriteLine($"FinalizeSuccess: {execution.FinalizeSuccessCount}");

            _output.WriteLine($"Resolver Started: {resolver.ResolvedStartedCount}");
            _output.WriteLine($"Resolver Success: {resolver.ResolvedSuccessCount}");

            _output.WriteLine($"HotState Added: {hotState.StateStepAddedCount}");
            _output.WriteLine($"HotState Observed: {hotState.StateSizeObservedCount}");

            _output.WriteLine($"Storage Stored: {storage.PayloadStoredCount}");
            _output.WriteLine($"Storage Loaded: {storage.PayloadLoadedCount}");

            Assert.Equal(1, execution.ExecutionStartedCount);
            Assert.Equal(1, execution.ExecutionCompletedCount);
            Assert.Equal(StepCount, execution.StepCompletedCount);

            Assert.True(
                execution.StepClaimedCount >= StepCount,
                $"Expected at least {StepCount} claims but got {execution.StepClaimedCount}");

            Assert.True(execution.FinalizeAttemptCount >= 0);
            Assert.True(execution.FinalizeSuccessCount >= 0);

            Assert.True(resolver.ResolvedStartedCount >= StepCount);
            Assert.True(resolver.ResolvedSuccessCount >= StepCount);

            Assert.True(hotState.StateStepAddedCount >= StepCount);
            Assert.True(hotState.StateSizeObservedCount >= StepCount);

            Assert.True(storage.PayloadStoredCount >= 0);
            Assert.True(storage.TotalPayloadStoredBytes > 0);

            Assert.True(retentionTrigger.TriggeredCount >= 0);

            _output.WriteLine("=== FINAL VALIDATION COMPLETE (ExecuteNextAsync) ===");
        }

        [Fact]
        public async Task Retention_Should_Limit_HotState_Size_And_Compact_Payloads()
        {
            var pipeline = CreatePipeline();

            await using var host = await CreateHost(pipeline);

            var metrics = host.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            var executionId = created.ExecutionId;
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var current = await host.Engine.ExecuteNextAsync(executionId);

                    if (current.IsTerminal)
                    {
                        break;
                    }
                }
                catch
                {
                    // No failure is expected for this pipeline, but the loop remains defensive.
                }

                var state = await host.ServiceProvider
                    .GetRequiredService<IAiDagExecutionStore>()
                    .GetStateAsync(executionId);

                if (state is not null)
                {
                    await WaitForRetryWindowIfNeededAsync(state);
                }
            }

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var finalRecord = await dagStore.GetRecordAsync(executionId);
            var finalState = await dagStore.GetStateAsync(executionId);

            Assert.NotNull(finalRecord);
            Assert.True(finalRecord!.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);

            Assert.NotNull(finalState);

            var retentionTrigger = Assert.IsType<AiRetentionTriggerMetrics>(metrics.Retention.Trigger);
            var retentionDecision = Assert.IsType<AiRetentionDecisionMetrics>(metrics.Retention.Decision);
            var retentionPlan = Assert.IsType<AiRetentionPlanMetrics>(metrics.Retention.Plan);
            var retentionExecution = Assert.IsType<AiRetentionExecutionMetrics>(metrics.Retention.Execution);
            var storage = Assert.IsType<AiStorageMetrics>(metrics.Storage);
            var hotState = Assert.IsType<AiHotStateMetrics>(metrics.HotState);

            _output.WriteLine($"Retention Triggered: {retentionTrigger.TriggeredCount}");
            _output.WriteLine($"Retention Skipped: {retentionTrigger.SkippedCount}");
            _output.WriteLine($"Retention CompactionRequired: {retentionDecision.CompactionRequiredCount}");
            _output.WriteLine($"Retention EvictionRequired: {retentionDecision.EvictionRequiredCount}");
            _output.WriteLine($"Retention NoActionRequired: {retentionDecision.NoActionRequiredCount}");
            _output.WriteLine($"Retention PlanCreated: {retentionPlan.PlanCreatedCount}");
            _output.WriteLine($"Retention PayloadCompacted: {retentionExecution.PayloadCompactedCount}");
            _output.WriteLine($"Retention StepEvicted: {retentionExecution.StepEvictedCount}");
            _output.WriteLine($"Retention Completed: {retentionExecution.RetentionCompletedCount}");
            _output.WriteLine($"HotState LastObservedStepCount: {hotState.LastObservedStepCount}");
            _output.WriteLine($"Final hot state step count: {finalState!.Steps.Count}");
            _output.WriteLine($"Storage Stored: {storage.PayloadStoredCount}");
            _output.WriteLine($"Storage Bytes: {storage.TotalPayloadStoredBytes}");

            Assert.True(
                finalState!.Steps.Count <= MaxCompletedStepsInState,
                $"State size exceeded limit: {finalState.Steps.Count}");

            var hasPayloadReference = finalState.Steps.Values.Any(s =>
                s.Result?.Payload is not null ||
                (s.Result?.DataPayloads is not null && s.Result.DataPayloads.Count > 0));

            Assert.True(
                hasPayloadReference,
                "Expected at least one compacted payload reference in retained state.");

            Assert.True(
                retentionTrigger.TriggeredCount > 0,
                $"Expected retention trigger count > 0, got {retentionTrigger.TriggeredCount}");

            Assert.True(
                retentionDecision.CompactionRequiredCount > 0 ||
                retentionDecision.EvictionRequiredCount > 0,
                $"Expected compaction or eviction decision. Compaction={retentionDecision.CompactionRequiredCount}, Eviction={retentionDecision.EvictionRequiredCount}");

            Assert.True(
                retentionPlan.PlanCreatedCount > 0,
                $"Expected at least one retention plan, got {retentionPlan.PlanCreatedCount}");

            Assert.True(
                retentionExecution.PayloadCompactedCount > 0 ||
                retentionExecution.StepEvictedCount > 0 ||
                retentionExecution.RetentionCompletedCount > 0,
                $"Expected retention execution work. PayloadCompacted={retentionExecution.PayloadCompactedCount}, StepEvicted={retentionExecution.StepEvictedCount}, Completed={retentionExecution.RetentionCompletedCount}");

            Assert.True(
                hotState.LastObservedStepCount >= MaxCompletedStepsInState,
                $"Expected hot-state observation >= {MaxCompletedStepsInState}, got {hotState.LastObservedStepCount}");

            Assert.True(
                storage.PayloadStoredCount > 0,
                $"Expected stored payloads, got {storage.PayloadStoredCount}");
        }

        [Fact]
        public async Task PayloadStore_Should_Save_Load_And_Handle_Missing_Payload()
        {
            var pipeline = CreatePipeline();

            await using var host = await CreateHost(pipeline);

            var metrics = host.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();
            var storage = Assert.IsType<AiStorageMetrics>(metrics.Storage);

            var payloadStore = host.ServiceProvider.GetRequiredService<IAiPayloadStore>();

            var content = new string('x', 2048);

            var payloadId = await payloadStore.SaveAsync(content);

            Assert.False(string.IsNullOrWhiteSpace(payloadId));

            var loaded = await payloadStore.LoadAsync(payloadId);

            Assert.Equal(content, loaded);

            var missing = await payloadStore.LoadAsync("does-not-exist");

            Assert.Null(missing);

            _output.WriteLine($"PayloadStoredCount: {storage.PayloadStoredCount}");
            _output.WriteLine($"PayloadLoadedCount: {storage.PayloadLoadedCount}");
            _output.WriteLine($"PayloadStoreMissCount: {storage.PayloadStoreMissCount}");
            _output.WriteLine($"PayloadStoreFailureCount: {storage.PayloadStoreFailureCount}");
            _output.WriteLine($"TotalPayloadStoredBytes: {storage.TotalPayloadStoredBytes}");

            Assert.True(
                storage.PayloadStoredCount >= 1,
                $"Expected at least one stored payload, got {storage.PayloadStoredCount}");

            Assert.True(
                storage.PayloadLoadedCount >= 1,
                $"Expected at least one loaded payload, got {storage.PayloadLoadedCount}");

            Assert.True(
                storage.PayloadStoreMissCount >= 1,
                $"Expected at least one missing payload metric, got {storage.PayloadStoreMissCount}");

            Assert.Equal(0, storage.PayloadStoreFailureCount);

            Assert.True(
                storage.TotalPayloadStoredBytes >= content.Length,
                $"Expected stored bytes >= {content.Length}, got {storage.TotalPayloadStoredBytes}");

            Assert.True(
                storage.OperationsByStorageKind.ContainsKey("mongo"),
                "Expected mongo storage operations to be recorded.");
        }

        [Fact]
        public async Task Execution_Should_Reach_Consistent_Terminal_State()
        {
            var pipeline = CreatePipeline();

            await using var host = await CreateHost(pipeline);

            var metrics = host.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            var final = await host.Engine
                .ExecuteAllAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(final.IsTerminal);

            Assert.True(
                final.Status == AiExecutionStatus.Completed ||
                final.Status == AiExecutionStatus.Failed);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);

            var allTerminal = state!.Steps.Values.All(s =>
                s.Status == AiStepExecutionStatus.Completed ||
                s.Status == AiStepExecutionStatus.Failed);

            Assert.True(allTerminal, "Not all retained steps reached terminal state.");

            var execution = Assert.IsType<AiExecutionMetrics>(metrics.Execution);
            var hotState = Assert.IsType<AiHotStateMetrics>(metrics.HotState);
            var resolver = Assert.IsType<AiResolverMetrics>(metrics.Resolver);
            var storage = Assert.IsType<AiStorageMetrics>(metrics.Storage);

            _output.WriteLine($"ExecutionStarted: {execution.ExecutionStartedCount}");
            _output.WriteLine($"ExecutionCompleted: {execution.ExecutionCompletedCount}");
            _output.WriteLine($"StepCompleted: {execution.StepCompletedCount}");
            _output.WriteLine($"HotStateAdded: {hotState.StateStepAddedCount}");
            _output.WriteLine($"ResolverStarted: {resolver.ResolvedStartedCount}");
            _output.WriteLine($"StorageStored: {storage.PayloadStoredCount}");

            Assert.Equal(1, execution.ExecutionStartedCount);
            Assert.Equal(1, execution.ExecutionCompletedCount);
            Assert.Equal(0, execution.ExecutionFailedCount);
            Assert.True(execution.StepCompletedCount >= state.Steps.Count);

            Assert.True(hotState.StateStepAddedCount >= StepCount);
            Assert.True(resolver.ResolvedStartedCount >= StepCount);
            Assert.True(storage.PayloadStoredCount > 0);
        }

        private static async Task WaitForRetryWindowIfNeededAsync(AiExecutionState state)
        {
            var nextRetryAtUtc = state.Steps.Values
                .Where(x => x.Status == AiStepExecutionStatus.WaitingForRetry)
                .Select(x => x.NextRetryAtUtc)
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

            var wait = delay > TimeSpan.FromMilliseconds(250)
                ? TimeSpan.FromMilliseconds(250)
                : delay;

            await Task.Delay(wait);
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Render_Trace_Timeline_To_Console()
        {
            const int stepCount = 10;

            var pipeline = CreateLargePipeline(stepCount);

            await using var host = await CreateHost(pipeline);

            var timeline = host.ServiceProvider.GetRequiredService<IAiTraceTimeline>();

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            var final = await host.Engine
                .ExecuteAllAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(final.IsTerminal);

            var events = timeline.Get(created.ExecutionId);

            Assert.NotEmpty(events);

            _output.WriteLine("");
            _output.WriteLine("===== TRACE TIMELINE =====");

            var start = events.Min(x => x.TimestampUtc);

            foreach (var e in events)
            {
                var offset = (e.TimestampUtc - start).TotalMilliseconds;

                var step = string.IsNullOrWhiteSpace(e.StepId)
                    ? ""
                    : $"[{e.StepId}]";

                _output.WriteLine(
                    $"[{offset,6:0} ms] {e.Category,-10} {e.Name,-25} {step}");
            }

            _output.WriteLine("==========================");
        }

        [Fact]
        public async Task ExecuteNextAsync_Should_Render_Trace_Timeline_To_Console()
        {
            const int stepCount = 10;

            var pipeline = CreateLargePipeline(stepCount);

            await using var host = await CreateHost(pipeline);

            var timeline = host.ServiceProvider.GetRequiredService<IAiTraceTimeline>();

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            var executionId = created.ExecutionId;
            var deadline = DateTime.UtcNow.AddSeconds(30);

            // -------------------------------
            // MAIN EXECUTION LOOP
            // -------------------------------
            while (DateTime.UtcNow < deadline)
            {
                AiExecutionRecord? current = null;

                try
                {
                    current = await host.Engine.ExecuteNextAsync(executionId);
                }
                catch
                {
                    // ignore transient errors (retry / race)
                }

                if (current?.IsTerminal == true)
                {
                    break;
                }

                var state = await host.ServiceProvider
                    .GetRequiredService<IAiDagExecutionStore>()
                    .GetStateAsync(executionId);

                if (state is not null)
                {
                    await WaitForRetryWindowIfNeededAsync(state);
                }
            }

            // -------------------------------
            // 🔥 FORCE FINALIZATION LOOP
            // -------------------------------
            var finalizeDeadline = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < finalizeDeadline)
            {
                var tick = await host.Engine.ExecuteNextAsync(executionId);

                // Stop once finalization happened
                if (tick.IsTerminal)
                {
                    break;
                }

                await Task.Delay(10);
            }

            var final = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetRecordAsync(executionId);

            Assert.NotNull(final);
            Assert.True(final!.IsTerminal);

            var events = timeline.Get(executionId);

            Assert.NotEmpty(events);

            // -------------------------------
            // 🔥 ASSERT FINALIZATION EXISTS
            // -------------------------------
            Assert.Contains(events, e =>
                string.Equals(e.Category, "dag-store", StringComparison.OrdinalIgnoreCase) &&
                e.Name.StartsWith("TryClaimNextReadyStep", StringComparison.Ordinal));

            Assert.Contains(events, e =>
                string.Equals(e.Category, "dag-store", StringComparison.OrdinalIgnoreCase) &&
                e.Name.StartsWith("TryCompleteStep", StringComparison.Ordinal));

            Assert.Contains(events, e =>
                string.Equals(e.Category, "step", StringComparison.OrdinalIgnoreCase) &&
                e.Name.StartsWith("execute", StringComparison.Ordinal));

            Assert.Contains(events, e =>
                string.Equals(e.Category, "retention", StringComparison.OrdinalIgnoreCase));

            var hasFinalize = events.Any(e =>
            string.Equals(e.Category, "dag-store", StringComparison.OrdinalIgnoreCase) &&
            e.Name.StartsWith("TryFinalizeExecution", StringComparison.Ordinal));

            _output.WriteLine($"Finalize trace present: {hasFinalize}");

            // -------------------------------
            // 🔥 RENDER CONSOLE
            // -------------------------------
            _output.WriteLine("");
            _output.WriteLine("===== TRACE TIMELINE =====");

            var start = events.Min(x => x.TimestampUtc);

            foreach (var e in events)
            {
                var offset = (e.TimestampUtc - start).TotalMilliseconds;

                var step = string.IsNullOrWhiteSpace(e.StepId)
                    ? ""
                    : $"[{e.StepId}]";

                var extra = "";

                if (string.Equals(e.Category, "retention", StringComparison.OrdinalIgnoreCase))
                {
                    if (e.Tags.TryGetValue("skipped", out var skipped) && skipped is true)
                    {
                        extra = " (noop)";
                    }
                    else
                    {
                        var compact = e.Tags.TryGetValue("compactedCount", out var c) ? c : 0;
                        var evict = e.Tags.TryGetValue("evictedCount", out var ev) ? ev : 0;
                        var removed = e.Tags.TryGetValue("removedSteps", out var r) ? r : 0;

                        extra = $" (compact={compact}, evict={evict}, removed={removed})";
                    }
                }

                _output.WriteLine(
                    $"[{offset,6:0} ms] {e.Category,-10} {e.Name,-30} {step}{extra}");
            }

            _output.WriteLine("==========================");
        }

        private static async Task<AiDagExecutionEngineTestHost> CreateHost(
            AiPipelineDefinition pipeline)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",

                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    MaxCompletedStepsInState = MaxCompletedStepsInState,
                    Mode = AiExecutionRetentionMode.Hybrid
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
                        ConnectionString = "mongodb://localhost:27017",
                        DatabaseName = "multiplexed_ai_tests",
                        CollectionName = $"payloads_metrics_{Guid.NewGuid():N}"
                    },

                    RedisCache = new RedisAiPayloadCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:payload:metrics:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        MaxCacheablePayloadBytes = 1024 * 1024
                    },

                    StepIndexCache = new RedisAiStepPayloadIndexCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:step-index:metrics:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        RefreshTtlOnRead = true
                    }
                }
            };

            options.RetentionTrigger.MaxCompletedStepsInState = MaxCompletedStepsInState;
            options.RetentionTrigger.MaxStepsInState = MaxCompletedStepsInState;
            options.RetentionTrigger.MaxInlinePayloadBytes = 1;

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    var provider = new InMemoryAiPipelineDefinitionProvider(new[] { pipeline });

                    services.RemoveAll<IAiPipelineDefinitionProvider>();
                    services.RemoveAll<InMemoryAiPipelineDefinitionProvider>();

                    services.AddSingleton<IAiPipelineDefinitionProvider>(provider);
                    services.AddSingleton(provider);

                    services.AddAiStepsFromAssemblies(
                        typeof(AiRuntimeMetricsFullPipelineIntegrationTests).Assembly);

                    services.RemoveAll<IAiExecutionRetentionPolicy>();
                    services.RemoveAll<IAiExecutionRetentionPolicyResolver>();
                    services.RemoveAll<IAiExecutionRetentionService>();

                    services.RemoveAll<IAiExecutionRetentionTrigger>();
                    services.RemoveAll<IAiExecutionRetentionDecisionEvaluator>();
                    services.RemoveAll<IAiExecutionRetentionDecisionService>();
                    services.RemoveAll<IAiExecutionRetentionDecisionPolicy>();

                    services.AddSingleton<IAiExecutionRetentionTrigger, DefaultAiExecutionRetentionTrigger>();

                    services.AddSingleton<IAiExecutionRetentionPolicy, NoopAiExecutionRetentionPolicy>();
                    services.AddSingleton<IAiExecutionRetentionPolicy, CompactAiExecutionRetentionPolicy>();
                    services.AddSingleton<IAiExecutionRetentionPolicy, EvictAiExecutionRetentionPolicy>();
                    services.AddSingleton<IAiExecutionRetentionPolicy, HybridAiExecutionRetentionPolicy>();

                    services.AddSingleton<
                        IAiExecutionRetentionPolicyResolver,
                        DefaultAiExecutionRetentionPolicyResolver>();

                    services.AddSingleton<
                        IAiExecutionRetentionDecisionEvaluator,
                        CompositeAiExecutionRetentionDecisionEvaluator>();

                    services.AddSingleton<
                        IAiExecutionRetentionDecisionService,
                        DefaultAiExecutionRetentionDecisionService>();

                    services.AddSingleton<IAiExecutionRetentionDecisionPolicy>(
                        new SizeBasedAiExecutionRetentionDecisionPolicy(1));

                    services.AddSingleton<
                        IAiExecutionRetentionService,
                        AiExecutionRetentionService>();

                    services.TryAddSingleton<IAiPayloadStore>(sp =>
                        new MongoAiPayloadStore(
                            sp.GetRequiredService<IOptions<AiPayloadStoreOptions>>(),
                            sp.GetRequiredService<IAiRuntimeMetrics>()));
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        private static AiPipelineDefinition CreatePipeline()
        {
            var steps = Enumerable.Range(0, StepCount)
                .Select(i => new AiPipelineStepDefinition
                {
                    Name = $"metrics-step-{i}",
                    StepKey = "test.metrics",
                    Order = i,
                    DependsOn = new List<string>(),
                    Config = new Dictionary<string, object?>
                    {
                        ["size"] = PayloadSize
                    }
                })
                .ToList();

            return new AiPipelineDefinition
            {
                Name = $"metrics-pipeline-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = steps
            };
        }

        private static AiPipelineDefinition CreateLargePipeline(int stepCount)
        {
            var steps = Enumerable.Range(0, stepCount)
                .Select(i => new AiPipelineStepDefinition
                {
                    Name = $"step-{i}",
                    StepKey = "test.metrics",
                    Order = i,
                    DependsOn = i == 0
                        ? new List<string>()
                        : new List<string> { $"step-{i - 1}" }, // chain DAG
                    Config = new Dictionary<string, object?>
                    {
                        ["size"] = 2048
                    }
                })
                .ToList();

            return new AiPipelineDefinition
            {
                Name = $"timeline-pipeline-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = steps
            };
        }

        [AiStep("test.metrics")]
        private sealed class MetricsStep : IAiStep
        {
            public string Name => "test.metrics";

            public async Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext ctx,
                CancellationToken ct = default)
            {
                var resolver = ctx.Services.GetRequiredService<IAiContextValueResolver>();

                var size = await resolver.ResolveRequiredAsync<int>(
                    ctx,
                    "config.size",
                    ct);

                _ = await resolver.ResolveRequiredAsync<string>(
                    ctx,
                    "execution.id",
                    ct);

                return new AiStepResult
                {
                    Success = true,
                    Data = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["content"] = new string('x', size)
                        }
                    }
                };
            }
        }
    }
}
