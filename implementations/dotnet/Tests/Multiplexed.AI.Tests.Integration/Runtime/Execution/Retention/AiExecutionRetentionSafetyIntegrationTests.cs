using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retention
{
    /// <summary>
    /// Validates the migration from legacy options-driven retention to the new
    /// config-driven, policy-driven retention engine.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate that retention behavior is driven from pipeline configuration.
    /// - Validate compact, evict, and hybrid retention policies through the DAG runtime.
    /// - Validate that evicted steps remain archived and resolvable.
    /// - Validate that the new policy-driven retention path replaces the legacy options path.
    ///
    /// IMPORTANT:
    /// - These tests intentionally do not use <c>AiExecutionStateRetentionOptions</c>.
    /// - These tests intentionally do not use the legacy <c>IAiExecutionRetentionService</c>.
    /// - Retention configuration is supplied through <see cref="AiPipelineDefinition.Config"/>.
    /// - The DAG runtime invokes the new retention engine through the policy engine factory.
    /// </remarks>
    public sealed class AiPolicyDrivenRetentionMigrationIntegrationTests
    {
        private const int MaxCompletedStepsInState = 5;
        private const int DefaultMaxInlinePayloadBytes = 512;
        private const int SmallPayloadSize = 128;
        private const int LargePayloadSize = 4096;

        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPolicyDrivenRetentionMigrationIntegrationTests"/> class.
        /// </summary>
        /// <param name="output">The xUnit output helper.</param>
        public AiPolicyDrivenRetentionMigrationIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Validates that hybrid retention runs through the new config-driven retention engine.
        /// </summary>
        /// <remarks>
        /// EXPECTED:
        /// - Retention is resolved from pipeline config.
        /// - Hybrid retention applies both compaction and eviction intent.
        /// - Hot state remains bounded after execution.
        /// - At least one completed step is archived and resolvable through the resolver.
        /// </remarks>
        [Fact]
        public async Task HybridRetention_Should_Run_Through_Config_Driven_Engine_Safely()
        {
            var (state, host) = await RunPipelineWithHost(
                stepCount: 20,
                policies:
                [
                    "retention.compact.terminal",
                    "retention.evict.terminal"
                ],
                payloadSize: LargePayloadSize);

            try
            {
                LogState(
                    nameof(HybridRetention_Should_Run_Through_Config_Driven_Engine_Safely),
                    state);

                Assert.NotNull(state);

                Assert.True(
                    state.Steps.Count <= MaxCompletedStepsInState,
                    $"Expected hot state to remain bounded. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}.");

                var evictedStepName = FindEvictedStepName(state, 20);

                Assert.False(
                    string.IsNullOrWhiteSpace(evictedStepName),
                    "Expected at least one completed step to be evicted from hot state.");

                var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

                var resolvedStep = await resolver
                    .GetStepAsync(state.ExecutionId, evictedStepName!, state)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(resolvedStep);
                Assert.True(resolvedStep!.IsCompleted);
                Assert.NotNull(resolvedStep.Result);
                Assert.True(resolvedStep.Result.Success);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that eviction persists archived step payloads before removing steps from hot state.
        /// </summary>
        /// <remarks>
        /// EXPECTED:
        /// - Eviction is driven from pipeline config.
        /// - Completed steps are removed from hot state.
        /// - Each removed step has an archived index entry.
        /// - Each archived index entry points to a persisted external step payload.
        /// </remarks>
        [Fact]
        public async Task Evict_Should_Persist_Archived_Payload_Before_Removing_From_Hot_State()
        {
            var (state, host) = await RunPipelineWithHost(
                stepCount: 20,
                policies:
                [
                    "retention.evict.terminal"
                ],
                payloadSize: SmallPayloadSize);

            try
            {
                LogState(
                    nameof(Evict_Should_Persist_Archived_Payload_Before_Removing_From_Hot_State),
                    state);

                Assert.NotNull(state);

                Assert.True(
                    state.Steps.Count <= MaxCompletedStepsInState,
                    $"Expected hot state to remain bounded. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}.");

                var evictedStepNames = FindEvictedStepNames(state, 20).ToArray();

                Assert.NotEmpty(evictedStepNames);

                var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();
                var stepPayloadStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadStore>();

                foreach (var stepName in evictedStepNames)
                {
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
                    Assert.True(restoredStep.Result.Success);
                }
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that archived / evicted steps remain resolvable through the execution step resolver.
        /// </summary>
        /// <remarks>
        /// EXPECTED:
        /// - Evicted steps are absent from hot state.
        /// - Evicted steps remain resolvable from external payload storage.
        /// - Resolved archived steps preserve their completed result.
        /// </remarks>
        [Fact]
        public async Task Archived_Steps_Should_Remain_Resolvable_After_Eviction()
        {
            var (state, host) = await RunPipelineWithHost(
                stepCount: 20,
                policies:
                [
                    "retention.evict.terminal"
                ],
                payloadSize: SmallPayloadSize);

            try
            {
                LogState(
                    nameof(Archived_Steps_Should_Remain_Resolvable_After_Eviction),
                    state);

                Assert.NotNull(state);

                var evictedStepName = FindEvictedStepName(state, 20);

                Assert.False(
                    string.IsNullOrWhiteSpace(evictedStepName),
                    "Expected at least one step to be evicted from hot state.");

                var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

                var resolvedStep = await resolver
                    .GetStepAsync(state.ExecutionId, evictedStepName!, state)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(resolvedStep);
                Assert.True(resolvedStep!.IsCompleted);
                Assert.NotNull(resolvedStep.Result);
                Assert.True(resolvedStep.Result.Success);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that hybrid retention preserves both policy decisions:
        /// compact first, then evict.
        /// </summary>
        /// <remarks>
        /// EXPECTED:
        /// - Hybrid policy selects terminal steps for both compaction and eviction.
        /// - Runtime applies compaction before eviction.
        /// - Evicted steps remain archived and resolvable.
        /// - Archived step state no longer contributes inline payload pressure.
        /// </remarks>
        [Fact]
        public async Task Hybrid_Should_Apply_Compaction_Before_Eviction()
        {
            var (state, host) = await RunPipelineWithHost(
                stepCount: 20,
                policies:
                [
                    "retention.compact.terminal",
                    "retention.evict.terminal"
                ],
                payloadSize: LargePayloadSize);

            try
            {
                LogState(
                    nameof(Hybrid_Should_Apply_Compaction_Before_Eviction),
                    state);

                Assert.NotNull(state);

                Assert.True(
                    state.Steps.Count <= MaxCompletedStepsInState,
                    $"Expected bounded hot state. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}.");

                var evictedStepName = FindEvictedStepName(state, 20);

                Assert.False(
                    string.IsNullOrWhiteSpace(evictedStepName),
                    "Expected at least one step to be evicted from hot state.");

                var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

                var resolvedStep = await resolver
                    .GetStepAsync(state.ExecutionId, evictedStepName!, state)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(resolvedStep);
                Assert.True(resolvedStep!.IsCompleted);
                Assert.NotNull(resolvedStep.Result);
                Assert.True(resolvedStep.Result.Success);

                Assert.Null(resolvedStep.InlinePayloadSizeBytes);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that running the terminal execution path again remains safe.
        /// </summary>
        /// <remarks>
        /// EXPECTED:
        /// - First execution applies retention.
        /// - Re-entering the execution path does not corrupt hot state.
        /// - Archived steps remain resolvable after the second call.
        /// </remarks>
        [Fact]
        public async Task Retention_Should_Be_Idempotent_Through_Runtime_Reentry()
        {
            var (state, host) = await RunPipelineWithHost(
                stepCount: 20,
                policies:
                [
                    "retention.compact.terminal",
                    "retention.evict.terminal"
                ],
                payloadSize: LargePayloadSize);

            try
            {
                LogState(
                    nameof(Retention_Should_Be_Idempotent_Through_Runtime_Reentry),
                    state);

                var stepsAfterFirstRetention = state.Steps.Count;
                var evictedStepName = FindEvictedStepName(state, 20);

                Assert.False(
                    string.IsNullOrWhiteSpace(evictedStepName),
                    "Expected at least one step to be evicted after first retention.");

                await host.Engine
                    .ExecuteAllAsync(state.ExecutionId)
                    .WaitAsync(TimeSpan.FromSeconds(30));

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var stateAfterSecondRun = await dagStore
                    .GetStateAsync(state.ExecutionId)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(stateAfterSecondRun);

                Assert.True(
                    stateAfterSecondRun!.Steps.Count <= stepsAfterFirstRetention,
                    $"Expected second runtime entry not to increase hot state. Before={stepsAfterFirstRetention}, After={stateAfterSecondRun.Steps.Count}.");

                var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

                var resolved = await resolver
                    .GetStepAsync(stateAfterSecondRun.ExecutionId, evictedStepName!, stateAfterSecondRun)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(resolved);
                Assert.True(resolved!.IsCompleted);
                Assert.NotNull(resolved.Result);
                Assert.True(resolved.Result.Success);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that a retained execution can be reloaded and still resolve archived steps.
        /// </summary>
        /// <remarks>
        /// EXPECTED:
        /// - Retention evicts completed steps.
        /// - State is reloaded from the DAG execution store.
        /// - Archived steps are still resolvable after reload.
        /// - Resolved step keeps its completed result.
        /// </remarks>
        [Fact]
        public async Task Retention_Replayed_State_Should_Resolve_Archived_Steps()
        {
            var (state, host) = await RunPipelineWithHost(
                stepCount: 20,
                policies:
                [
                    "retention.compact.terminal",
                    "retention.evict.terminal"
                ],
                payloadSize: SmallPayloadSize);

            try
            {
                LogState(
                    nameof(Retention_Replayed_State_Should_Resolve_Archived_Steps),
                    state);

                var evictedStepName = FindEvictedStepName(state, 20);

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
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Runs a DAG pipeline and returns the final execution state and test host.
        /// </summary>
        private async Task<(AiExecutionState State, AiDagExecutionEngineTestHost Host)> RunPipelineWithHost(
            int stepCount,
            IReadOnlyCollection<string> policies,
            int payloadSize,
            int maxInlinePayloadBytes = DefaultMaxInlinePayloadBytes)
        {
            var pipeline = CreateFullyParallelPipeline(
                stepCount,
                policies,
                payloadSize,
                maxInlinePayloadBytes);

            var host = await CreateHost(pipeline);

            _output.WriteLine(
                $"Pipeline='{pipeline.Name}', Steps='{stepCount}', Policies='{string.Join(",", policies)}', PayloadSize='{payloadSize}', MaxInlinePayloadBytes='{maxInlinePayloadBytes}'.");

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            _output.WriteLine($"Execution created. ExecutionId='{created.ExecutionId}'.");

            await host.Engine
                .ExecuteAllAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(30));

            _output.WriteLine($"Execution completed. ExecutionId='{created.ExecutionId}'.");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore
                .GetStateAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(state);

            _output.WriteLine($"State loaded. StepsAfterRetention='{state!.Steps.Count}'.");

            return (state, host);
        }

        /// <summary>
        /// Creates a production-like DAG engine test host for policy-driven retention validation.
        /// </summary>
        private static async Task<AiDagExecutionEngineTestHost> CreateHost(
            AiPipelineDefinition pipeline)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory"
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
                        typeof(AiPolicyDrivenRetentionMigrationIntegrationTests).Assembly);
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        /// <summary>
        /// Creates a fully parallel DAG pipeline where completed steps are safe retention candidates.
        /// </summary>
        private static AiPipelineDefinition CreateFullyParallelPipeline(
            int steps,
            IReadOnlyCollection<string> policies,
            int payloadSize,
            int maxInlinePayloadBytes)
        {
            var list = new List<AiPipelineStepDefinition>();

            for (var i = 0; i < steps; i++)
            {
                list.Add(new AiPipelineStepDefinition
                {
                    Name = $"step-{i}",
                    StepKey = "test.policy-driven-retention.safety",
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
                Name = $"policy-driven-retention-safety-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Config = CreateRetentionConfig(
                    policies,
                    maxInlinePayloadBytes),
                Steps = list
            };
        }

        /// <summary>
        /// Creates pipeline-level retention configuration for the new policy-driven retention engine.
        /// </summary>
        private static Dictionary<string, object?> CreateRetentionConfig(
            IReadOnlyCollection<string> policies,
            int maxInlinePayloadBytes)
        {
            return new Dictionary<string, object?>
            {
                ["retention"] = new Dictionary<string, object?>
                {
                    ["enabled"] = true,
                    ["policies"] = policies.ToArray(),
                    ["archiveReason"] = "policy-driven-retention-test",
                    ["trigger"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["maxStepsInState"] = MaxCompletedStepsInState,
                        ["maxCompletedStepsInState"] = MaxCompletedStepsInState,
                        ["maxInlinePayloadBytes"] = maxInlinePayloadBytes
                    }
                }
            };
        }

        /// <summary>
        /// Finds the first step name that has been evicted from hot state.
        /// </summary>
        private static string? FindEvictedStepName(
            AiExecutionState state,
            int totalSteps)
        {
            return FindEvictedStepNames(state, totalSteps).FirstOrDefault();
        }

        /// <summary>
        /// Finds all step names that have been evicted from hot state.
        /// </summary>
        private static IEnumerable<string> FindEvictedStepNames(
            AiExecutionState state,
            int totalSteps)
        {
            for (var i = 0; i < totalSteps; i++)
            {
                var stepName = $"step-{i}";

                if (!state.Steps.ContainsKey(stepName))
                {
                    yield return stepName;
                }
            }
        }

        /// <summary>
        /// Writes current hot-state retention information to test output.
        /// </summary>
        private void LogState(
            string testName,
            AiExecutionState state)
        {
            _output.WriteLine(
                $"[{testName}] ExecutionId={state.ExecutionId}, HotSteps={state.Steps.Count}, Steps=[{string.Join(",", state.Steps.Keys.OrderBy(x => x, StringComparer.Ordinal))}]");
        }

        /// <summary>
        /// Test step producing a configurable payload size for policy-driven retention validation.
        /// </summary>
        [AiStep("test.policy-driven-retention.safety")]
        private sealed class PolicyDrivenRetentionSafetyStep : IAiStep
        {
            /// <summary>
            /// Gets the test step name.
            /// </summary>
            public string Name => "test.policy-driven-retention.safety";

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
