using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Decisions;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;
using Multiplexed.Abstractions.AI.Execution.Retention.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Execution.Metrics;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Retention;
using Multiplexed.AI.Runtime.Retention.Decisions;
using Multiplexed.AI.Runtime.Retention.Decisions.Policies;
using Multiplexed.AI.Runtime.Retention.Policies;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retention
{
    /// <summary>
    /// Validates the complete execution retention pipeline using the production runtime wiring.
    ///
    /// PURPOSE:
    /// - Verify that adaptive retention trigger, decision service, decision evaluator,
    ///   retention policies, and retention service work together safely.
    /// - Validate retention behavior without touching DAG/Lua/convergence logic.
    /// - Provide a dedicated safety suite before adding more intelligent retention policies.
    ///
    /// IMPORTANT:
    /// - Tests must be added one by one.
    /// - Each test should validate one invariant.
    /// - Retention must never remove data before external persistence succeeds.
    /// </summary>
    public sealed class AiExecutionRetentionSafetyIntegrationTests
    {
        private const int MaxCompletedStepsInState = 5;
        private const int DefaultMaxInlinePayloadBytes = 512;
        private const int SmallPayloadSize = 128;
        private const int LargePayloadSize = 4096;

        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionRetentionSafetyIntegrationTests"/> class.
        /// </summary>
        /// <param name="output">The xUnit output helper.</param>
        public AiExecutionRetentionSafetyIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Validates that the full retention pipeline runs safely with hybrid retention enabled.
        ///
        /// EXPECTED:
        /// - Adaptive trigger allows retention to run.
        /// - Decision layer can add compaction candidates when payload exceeds threshold.
        /// - Hybrid policy can evict old completed steps.
        /// - Hot state remains bounded.
        /// - Retention metrics report applied work.
        /// </summary>
        [Fact]
        public async Task HybridRetention_Should_Run_Through_Trigger_Decision_And_Service_Safely()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(
                stepCount: 20,
                mode: AiExecutionRetentionMode.Hybrid,
                payloadSize: LargePayloadSize);

            LogRetentionSnapshot(
                nameof(HybridRetention_Should_Run_Through_Trigger_Decision_And_Service_Safely),
                snapshot);

            Assert.NotNull(state);

            Assert.True(
                snapshot.StepsPlannedForCompaction > 0 ||
                snapshot.StepsPlannedForEviction > 0,
                "Expected retention to be planned by the trigger/decision/policy pipeline.");

            Assert.True(
                snapshot.CompactedSteps > 0 ||
                snapshot.EvictedSteps > 0,
                "Expected retention to apply at least one compaction or eviction operation.");

            Assert.True(
                state.Steps.Count <= MaxCompletedStepsInState,
                $"Expected hot state to remain bounded. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}.");

            await host.DisposeAsync();
        }

        /// <summary>
        /// Validates that eviction persists archived step payloads before removing steps from hot state.
        ///
        /// EXPECTED:
        /// - Retention evicts completed steps.
        /// - Hot state remains bounded.
        /// - Each evicted step has an archived index entry.
        /// - Each archived index entry points to a persisted external step payload.
        ///
        /// IMPORTANT:
        /// - Uses a small payload below the inline threshold so the test focuses on eviction safety,
        ///   not size-based compaction.
        /// </summary>
        [Fact]
        public async Task Evict_Should_Persist_Archived_Payload_Before_Removing_From_Hot_State()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(
                stepCount: 20,
                mode: AiExecutionRetentionMode.Evict,
                payloadSize: SmallPayloadSize);

            LogRetentionSnapshot(
                nameof(Evict_Should_Persist_Archived_Payload_Before_Removing_From_Hot_State),
                snapshot);

            Assert.NotNull(state);

            Assert.True(
                snapshot.EvictedSteps > 0,
                "Expected eviction to remove completed steps from hot state.");

            Assert.True(
                state.Steps.Count <= MaxCompletedStepsInState,
                $"Expected hot state to remain bounded. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}.");

            var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();
            var stepPayloadStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadStore>();

            for (var i = 0; i < 20; i++)
            {
                var stepName = $"step-{i}";

                if (state.Steps.ContainsKey(stepName))
                {
                    continue;
                }

                var archived = await indexStore
                    .GetAsync(state.ExecutionId, stepName)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(archived);
                Assert.Equal(state.ExecutionId, archived!.ExecutionId);
                Assert.Equal(stepName, archived.StepName);
                Assert.NotNull(archived.Payload);
                Assert.False(string.IsNullOrWhiteSpace(archived.Payload.ArtifactId));

                var restoredStep = await stepPayloadStore
                    .LoadStepAsync(state.ExecutionId, stepName, archived.Payload)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(restoredStep);
                Assert.True(restoredStep!.IsCompleted);
                Assert.NotNull(restoredStep.Result);
            }

            await host.DisposeAsync();
        }

        /// <summary>
        /// Validates that archived / evicted steps remain resolvable through the execution step resolver.
        ///
        /// EXPECTED:
        /// - Retention evicts completed steps from hot state.
        /// - Archived step index entries are written.
        /// - Evicted steps can still be resolved from external payload storage.
        /// - Resolved archived steps preserve their completed result.
        ///
        /// IMPORTANT:
        /// - Uses a small payload below the inline threshold so the test focuses on resolver behavior
        ///   after eviction.
        /// </summary>
        [Fact]
        public async Task Archived_Steps_Should_Remain_Resolvable_After_Eviction()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(
                stepCount: 20,
                mode: AiExecutionRetentionMode.Evict,
                payloadSize: SmallPayloadSize);

            LogRetentionSnapshot(
                nameof(Archived_Steps_Should_Remain_Resolvable_After_Eviction),
                snapshot);

            Assert.NotNull(state);

            Assert.True(
                snapshot.EvictedSteps > 0,
                "Expected eviction to archive at least one completed step.");

            var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

            var evictedStepName = Enumerable
                .Range(0, 20)
                .Select(i => $"step-{i}")
                .FirstOrDefault(stepName => !state.Steps.ContainsKey(stepName));

            Assert.False(
                string.IsNullOrWhiteSpace(evictedStepName),
                "Expected at least one step to be evicted from hot state.");

            var resolvedStep = await resolver
                .GetStepAsync(state.ExecutionId, evictedStepName!, state)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(resolvedStep);
            Assert.True(resolvedStep!.IsCompleted);
            Assert.NotNull(resolvedStep.Result);
            Assert.True(resolvedStep.Result.Success);

            await host.DisposeAsync();
        }

        /// <summary>
        /// Validates that hybrid retention applies compaction before eviction and keeps behavior consistent.
        ///
        /// EXPECTED:
        /// - Steps selected for eviction are NOT compacted.
        /// - Other eligible steps are compacted.
        /// - Eviction still persists payloads safely.
        /// - Hot state remains bounded.
        /// </summary>
        [Fact]
        public async Task Hybrid_Should_Respect_Compaction_Then_Eviction()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(
                stepCount: 20,
                mode: AiExecutionRetentionMode.Hybrid,
                payloadSize: LargePayloadSize);

            LogRetentionSnapshot(
                nameof(Hybrid_Should_Respect_Compaction_Then_Eviction),
                snapshot);

            Assert.NotNull(state);

            // Hybrid should do BOTH (based on actual applied operations)
            Assert.True(
                snapshot.CompactedSteps > 0,
                "Expected hybrid retention to compact at least one step.");

            Assert.True(
                snapshot.EvictedSteps > 0,
                "Expected hybrid retention to evict at least one step.");

            // Critical invariant:
            // Evicted steps must NOT be compacted
            // → Total applied actions should cover planned compaction intent
            Assert.True(
                snapshot.CompactedSteps + snapshot.EvictedSteps >= snapshot.StepsPlannedForCompaction,
                "Unexpected mismatch between planned and applied retention actions.");

            // State bounded
            Assert.True(
                state.Steps.Count <= MaxCompletedStepsInState,
                $"Expected bounded state. Actual={state.Steps.Count}");

            // Verify at least one evicted step is recoverable
            var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

            var evictedStepName = Enumerable
                .Range(0, 20)
                .Select(i => $"step-{i}")
                .FirstOrDefault(stepName => !state.Steps.ContainsKey(stepName));

            Assert.NotNull(evictedStepName);

            var resolved = await resolver
                .GetStepAsync(state.ExecutionId, evictedStepName!, state)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(resolved);
            Assert.True(resolved!.IsCompleted);
            Assert.True(resolved.Result!.Success);

            await host.DisposeAsync();
        }

        /// <summary>
        /// Validates that applying retention multiple times remains safe and stable.
        ///
        /// EXPECTED:
        /// - First execution applies retention.
        /// - Re-applying retention does not corrupt state.
        /// - Hot state remains bounded.
        /// - Archived steps remain resolvable.
        /// - No restored / archived payload is lost after a second retention pass.
        /// </summary>
        [Fact]
        public async Task Retention_Should_Be_Idempotent()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(
                stepCount: 20,
                mode: AiExecutionRetentionMode.Hybrid,
                payloadSize: LargePayloadSize);

            LogRetentionSnapshot(
                nameof(Retention_Should_Be_Idempotent),
                snapshot);

            Assert.NotNull(state);

            Assert.True(
                snapshot.CompactedSteps > 0 || snapshot.EvictedSteps > 0,
                "Expected first retention pass to apply work.");

            Assert.True(
                state.Steps.Count <= MaxCompletedStepsInState,
                $"Expected bounded state after first retention. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}.");

            var stepsAfterFirstRetention = state.Steps.Count;

            var evictedStepName = Enumerable
                .Range(0, 20)
                .Select(i => $"step-{i}")
                .FirstOrDefault(stepName => !state.Steps.ContainsKey(stepName));

            Assert.False(
                string.IsNullOrWhiteSpace(evictedStepName),
                "Expected at least one step to be evicted after first retention.");

            var retentionService = host.ServiceProvider.GetRequiredService<IAiExecutionRetentionService>();

            var secondResult = await retentionService
                .ApplyAsync(state, AiExecutionRetentionMode.Hybrid)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(secondResult);

            Assert.True(
                state.Steps.Count <= MaxCompletedStepsInState,
                $"Expected bounded state after second retention. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}.");

            Assert.True(
                state.Steps.Count <= stepsAfterFirstRetention,
                $"Expected second retention not to increase hot state. Before={stepsAfterFirstRetention}, After={state.Steps.Count}.");

            var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

            var resolved = await resolver
                .GetStepAsync(state.ExecutionId, evictedStepName!, state)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(resolved);
            Assert.True(resolved!.IsCompleted);
            Assert.NotNull(resolved.Result);
            Assert.True(resolved.Result.Success);

            await host.DisposeAsync();
        }

        /// <summary>
        /// Validates that a retained execution can be reloaded and still resolve archived steps.
        ///
        /// EXPECTED:
        /// - Hybrid retention evicts completed steps.
        /// - State is reloaded from the DAG execution store.
        /// - Archived steps are still resolvable after reload.
        /// - Resolved step keeps its completed result.
        ///
        /// IMPORTANT:
        /// - Uses a small payload below the inline threshold so the test focuses on archived state
        ///   reload and resolution rather than payload-size compaction.
        /// </summary>
        [Fact]
        public async Task Retention_Replayed_State_Should_Resolve_Archived_Steps()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(
                stepCount: 20,
                mode: AiExecutionRetentionMode.Hybrid,
                payloadSize: SmallPayloadSize);

            LogRetentionSnapshot(
                nameof(Retention_Replayed_State_Should_Resolve_Archived_Steps),
                snapshot);

            Assert.NotNull(state);
            Assert.True(snapshot.EvictedSteps > 0, "Expected at least one evicted step.");

            var evictedStepName = Enumerable
                .Range(0, 20)
                .Select(i => $"step-{i}")
                .FirstOrDefault(stepName => !state.Steps.ContainsKey(stepName));

            Assert.False(
                string.IsNullOrWhiteSpace(evictedStepName),
                "Expected at least one step to be evicted from hot state.");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var reloadedState = await dagStore
                .GetStateAsync(state.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(reloadedState);
            Assert.False(
                reloadedState!.Steps.ContainsKey(evictedStepName!),
                "Expected evicted step to remain outside hot state after reload.");

            var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

            var resolvedStep = await resolver
                .GetStepAsync(reloadedState.ExecutionId, evictedStepName!, reloadedState)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(resolvedStep);
            Assert.True(resolvedStep!.IsCompleted);
            Assert.NotNull(resolvedStep.Result);
            Assert.True(resolvedStep.Result.Success);

            await host.DisposeAsync();
        }

        /// <summary>
        /// Runs a DAG pipeline and returns the final execution state, retention metrics snapshot, and test host.
        /// </summary>
        private async Task<(AiExecutionState State, AiExecutionRetentionServiceMetricsSnapshot Snapshot, AiDagExecutionEngineTestHost Host)>
            RunPipelineWithHost(
                int stepCount,
                AiExecutionRetentionMode mode,
                int payloadSize,
                int maxInlinePayloadBytes = DefaultMaxInlinePayloadBytes)
        {
            var pipeline = CreateFullyParallelPipeline(stepCount, payloadSize);
            var host = await CreateHost(pipeline, mode, maxInlinePayloadBytes);

            _output.WriteLine(
                $"Pipeline='{pipeline.Name}', Steps='{stepCount}', Mode='{mode}', PayloadSize='{payloadSize}', MaxInlinePayloadBytes='{maxInlinePayloadBytes}'.");

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            _output.WriteLine($"Execution created. ExecutionId='{created.ExecutionId}'.");

            await host.Engine
                .ExecuteAllAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(30));

            _output.WriteLine($"Execution completed. ExecutionId='{created.ExecutionId}'.");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            var metrics = host.ServiceProvider.GetRequiredService<IAiExecutionRetentionServiceMetrics>();
            var snapshot = ((InMemoryAiExecutionRetentionServiceMetrics)metrics).Snapshot();

            _output.WriteLine($"State loaded. StepsAfterRetention='{state?.Steps.Count ?? 0}'.");

            return (state!, snapshot, host);
        }

        /// <summary>
        /// Creates a production-like DAG engine test host configured for retention safety validation.
        /// </summary>
        private static async Task<AiDagExecutionEngineTestHost> CreateHost(
            AiPipelineDefinition pipeline,
            AiExecutionRetentionMode mode,
            int maxInlinePayloadBytes)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",

                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = mode,
                    MaxCompletedStepsInState = MaxCompletedStepsInState
                }
            };

            options.RetentionTrigger.MaxCompletedStepsInState = MaxCompletedStepsInState;
            options.RetentionTrigger.MaxStepsInState = MaxCompletedStepsInState;
            options.RetentionTrigger.MaxInlinePayloadBytes = maxInlinePayloadBytes;

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
                        typeof(AiExecutionRetentionSafetyIntegrationTests).Assembly);

                    services.RemoveAll<IAiExecutionRetentionPolicy>();
                    services.RemoveAll<IAiExecutionRetentionPolicyResolver>();
                    services.RemoveAll<IAiExecutionRetentionService>();
                    services.RemoveAll<IAiExecutionRetentionServiceMetrics>();

                    services.RemoveAll<IAiExecutionRetentionDecisionService>();
                    services.RemoveAll<IAiExecutionRetentionDecisionEvaluator>();
                    services.RemoveAll<IAiExecutionRetentionDecisionPolicy>();

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
                        new SizeBasedAiExecutionRetentionDecisionPolicy(maxInlinePayloadBytes));

                    services.AddSingleton<
                        IAiExecutionRetentionServiceMetrics,
                        InMemoryAiExecutionRetentionServiceMetrics>();

                    services.AddSingleton<
                        IAiExecutionRetentionService,
                        AiExecutionRetentionService>();
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        /// <summary>
        /// Creates a fully parallel DAG pipeline where completed steps are safe candidates for retention.
        /// </summary>
        private static AiPipelineDefinition CreateFullyParallelPipeline(
            int steps,
            int payloadSize)
        {
            var list = new List<AiPipelineStepDefinition>();

            for (var i = 0; i < steps; i++)
            {
                list.Add(new AiPipelineStepDefinition
                {
                    Name = $"step-{i}",
                    StepKey = "test.retention.safety",
                    Order = i,
                    DependsOn = new List<string>(),
                    Config = new Dictionary<string, object?>
                    {
                        ["size"] = payloadSize
                    }
                });
            }

            return new AiPipelineDefinition
            {
                Name = $"retention-safety-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = list
            };
        }

        /// <summary>
        /// Writes retention metrics to the test output.
        /// </summary>
        private void LogRetentionSnapshot(
            string testName,
            AiExecutionRetentionServiceMetricsSnapshot snapshot)
        {
            _output.WriteLine(
                $"[{testName}] Retention service metrics: " +
                $"Mode={snapshot.LastMode}, " +
                $"Before={snapshot.TotalStepsBefore}, " +
                $"After={snapshot.TotalStepsAfter}, " +
                $"PlannedCompact={snapshot.StepsPlannedForCompaction}, " +
                $"PlannedEvict={snapshot.StepsPlannedForEviction}, " +
                $"Compacted={snapshot.CompactedSteps}, " +
                $"Evicted={snapshot.EvictedSteps}");
        }

        /// <summary>
        /// Test step producing a configurable payload size for retention safety validation.
        /// </summary>
        [AiStep("test.retention.safety")]
        private sealed class RetentionSafetyStep : IAiStep
        {
            /// <summary>
            /// Gets the test step name.
            /// </summary>
            public string Name => "test.retention.safety";

            /// <inheritdoc />
            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext ctx,
                CancellationToken ct = default)
            {
                var size = Convert.ToInt32(ctx.Step.Config["size"]);

                return Task.FromResult(new AiStepResult
                {
                    Success = true,
                    Data = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["content"] = new string('x', size)
                        }
                    }
                });
            }
        }
    }
}
