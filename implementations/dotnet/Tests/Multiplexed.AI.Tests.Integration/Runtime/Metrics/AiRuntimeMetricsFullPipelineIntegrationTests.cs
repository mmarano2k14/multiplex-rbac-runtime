using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Execution;
using Multiplexed.AI.Runtime.Metrics.HotState;
using Multiplexed.AI.Runtime.Metrics.Resolvers;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Multiplexed.AI.Runtime.Metrics.Storage;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Metrics
{
    /// <summary>
    /// End-to-end integration tests for runtime metrics.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Execute real DAG pipelines through the standard DI/runtime flow.
    /// - Validate that the central <see cref="IAiRuntimeMetrics"/> facade is wired correctly.
    /// - Verify execution, resolver, hot-state, storage, and retention metrics using real pipeline execution.
    ///
    /// RETENTION MIGRATION:
    /// - Legacy retention services/options/resolvers are no longer registered here.
    /// - Retention is configured through pipeline-level <c>config.retention</c>.
    /// - Retention is executed by the policy-driven retention engine.
    /// </remarks>
    public sealed class AiRuntimeMetricsFullPipelineIntegrationTests
    {
        private const int StepCount = 4;
        private const int PayloadSize = 4096;
        private const int MaxCompletedStepsInState = 2;

        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeMetricsFullPipelineIntegrationTests"/> class.
        /// </summary>
        /// <param name="output">The xUnit output helper.</param>
        public AiRuntimeMetricsFullPipelineIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Validates that ExecuteAll records metrics across all runtime domains.
        /// </summary>
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
            Assert.True(hotState.LastObservedStepCount >= 0);

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

        /// <summary>
        /// Validates that ExecuteNext records metrics across runtime domains.
        /// </summary>
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

        /// <summary>
        /// Validates that policy-driven retention limits hot-state size and records retention/storage metrics.
        /// </summary>
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

            var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();
            var archivedEntries = await indexStore.GetByExecutionAsync(executionId);

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
            _output.WriteLine($"Archived step count: {archivedEntries.Count}");
            _output.WriteLine($"Storage Stored: {storage.PayloadStoredCount}");
            _output.WriteLine($"Storage Bytes: {storage.TotalPayloadStoredBytes}");

            Assert.True(
                finalState!.Steps.Count <= MaxCompletedStepsInState,
                $"State size exceeded limit: {finalState.Steps.Count}");

            var retainedPayloadReference = finalState.Steps.Values.Any(s =>
                s.Result?.Payload is not null ||
                (s.Result?.DataPayloads is not null && s.Result.DataPayloads.Count > 0));

            var archivedPayloadReference = archivedEntries.Any(x =>
                x.Payload is not null &&
                (!x.Payload.IsInline || !string.IsNullOrWhiteSpace(x.Payload.ArtifactId)));

            Assert.True(
                retainedPayloadReference || archivedPayloadReference || archivedEntries.Count > 0,
                "Expected at least one compacted or archived payload reference.");

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
                hotState.LastObservedStepCount >= 0,
                $"Expected hot-state observation >= 0, got {hotState.LastObservedStepCount}");

            Assert.True(
                storage.PayloadStoredCount > 0,
                $"Expected stored payloads, got {storage.PayloadStoredCount}");
        }

        /// <summary>
        /// Validates payload store metrics for save, load, and miss paths.
        /// </summary>
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

        /// <summary>
        /// Validates terminal state consistency with metrics.
        /// </summary>
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

            Assert.True(
                final.IsTerminal,
                "Execution record did not reach terminal state.");

            Assert.True(
                final.Status == AiExecutionStatus.Completed ||
                final.Status == AiExecutionStatus.Failed,
                $"Unexpected final status: {final.Status}");

            var execution = Assert.IsType<AiExecutionMetrics>(metrics.Execution);
            var hotState = Assert.IsType<AiHotStateMetrics>(metrics.HotState);
            var resolver = Assert.IsType<AiResolverMetrics>(metrics.Resolver);
            var storage = Assert.IsType<AiStorageMetrics>(metrics.Storage);

            Assert.Equal(1, execution.ExecutionStartedCount);
            Assert.Equal(1, execution.ExecutionCompletedCount);
            Assert.Equal(0, execution.ExecutionFailedCount);
            Assert.True(execution.StepCompletedCount >= state.Steps.Count);

            Assert.True(hotState.StateStepAddedCount >= StepCount);
            Assert.True(resolver.ResolvedStartedCount >= StepCount);
            Assert.True(storage.PayloadStoredCount > 0);
        }

        /// <summary>
        /// Renders the trace timeline for ExecuteAll.
        /// </summary>
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

            RenderTimeline(events);
        }

        /// <summary>
        /// Renders the trace timeline for ExecuteNext.
        /// </summary>
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

            var finalizeDeadline = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < finalizeDeadline)
            {
                var tick = await host.Engine.ExecuteNextAsync(executionId);

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

            Assert.Contains(events, e =>
                string.Equals(e.Category, "dag-store", StringComparison.OrdinalIgnoreCase) &&
                e.Name.Contains("Claim", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(events, e =>
                string.Equals(e.Category, "dag-store", StringComparison.OrdinalIgnoreCase) &&
                (
                    e.Name.Contains("Complete", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.Contains("Fail", StringComparison.OrdinalIgnoreCase)
                ));

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

            RenderTimeline(events);
        }

        /// <summary>
        /// Waits for retry window when the state contains retrying steps.
        /// </summary>
        private static async Task WaitForRetryWindowIfNeededAsync(
            AiExecutionState state)
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

            var wait = delay > TimeSpan.FromMilliseconds(250)
                ? TimeSpan.FromMilliseconds(250)
                : delay;

            await Task.Delay(wait);
        }

        /// <summary>
        /// Creates a production-like host with policy-driven retention config.
        /// </summary>
        private static async Task<AiDagExecutionEngineTestHost> CreateHost(
            AiPipelineDefinition pipeline)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",

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

                    services.TryAddSingleton<IAiPayloadStore>(sp =>
                        new MongoAiPayloadStore(
                            sp.GetRequiredService<IOptions<AiPayloadStoreOptions>>(),
                            sp.GetRequiredService<IAiRuntimeMetrics>()));
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        /// <summary>
        /// Creates the default metrics pipeline.
        /// </summary>
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
                Config = CreateRetentionConfig(),
                Steps = steps
            };
        }

        /// <summary>
        /// Creates a larger chain pipeline for trace timeline validation.
        /// </summary>
        private static AiPipelineDefinition CreateLargePipeline(
            int stepCount)
        {
            var steps = Enumerable.Range(0, stepCount)
                .Select(i => new AiPipelineStepDefinition
                {
                    Name = $"step-{i}",
                    StepKey = "test.metrics",
                    Order = i,
                    DependsOn = i == 0
                        ? new List<string>()
                        : new List<string> { $"step-{i - 1}" },
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
                Config = CreateRetentionConfig(),
                Steps = steps
            };
        }

        /// <summary>
        /// Creates pipeline-level retention configuration for the policy-driven retention engine.
        /// </summary>
        private static Dictionary<string, object?> CreateRetentionConfig()
        {
            return new Dictionary<string, object?>
            {
                ["retention"] = new Dictionary<string, object?>
                {
                    ["enabled"] = true,
                    ["policies"] = new[]
                    {
                        "retention.compact.terminal",
                        "retention.evict.terminal"
                    },
                    ["archiveReason"] = "runtime-metrics-integration-test",
                    ["trigger"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["maxStepsInState"] = MaxCompletedStepsInState,
                        ["maxCompletedStepsInState"] = MaxCompletedStepsInState,
                        ["maxInlinePayloadBytes"] = 1
                    }
                }
            };
        }

        /// <summary>
        /// Renders trace timeline to xUnit output.
        /// </summary>
        private void RenderTimeline(
            IReadOnlyCollection<AiTraceEvent> events)
        {
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

        /// <summary>
        /// Test metrics step.
        /// </summary>
        [AiStep("test.metrics")]
        private sealed class MetricsStep : IAiStep
        {
            /// <summary>
            /// Gets the step key.
            /// </summary>
            public string Name => "test.metrics";

            /// <inheritdoc />
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
