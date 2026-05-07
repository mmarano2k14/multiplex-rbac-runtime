using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;
using Multiplexed.AI.Runtime.Execution.Retention.Policies;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retention
{
    /// <summary>
    /// Unit tests validating policy-driven retention selection behavior.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate retention policy selection behavior directly without engine/store noise.
    /// - Validate compact, evict, and hybrid retention candidate selection.
    /// - Validate deterministic retention policy outcomes.
    ///
    /// MIGRATION:
    /// - Legacy DefaultAiExecutionStateRetentionPolicy is removed.
    /// - Legacy retention metrics are removed.
    /// - Retention is now policy-driven through AiPolicyResultGeneric&lt;AiRetentionDecision&gt;.
    /// - These tests validate policy decisions directly.
    /// </remarks>
    public sealed class AiPolicyDrivenRetentionPolicyTests
    {
        /// <summary>
        /// Validates that compact retention selects all terminal steps.
        /// </summary>
        [Fact]
        public async Task CompactPolicy_Should_Select_All_Terminal_Steps()
        {
            var state = CreateLinearCompletedState(200);

            var policy = new CompactAiRetentionPolicy();

            var result = await policy.ExecuteAsync(
                new AiRetentionContext
                {
                    ExecutionState = state
                });

            var decision = GetDecision(result);

            Assert.Equal(AiRetentionDecisionKind.Compact, decision.Kind);

            Assert.NotNull(decision.StepsToCompact);
            Assert.Equal(200, decision.StepsToCompact.Count);

            Assert.Equal(200, state.Steps.Count);
        }

        /// <summary>
        /// Validates that eviction policy selects all terminal steps.
        /// </summary>
        [Fact]
        public async Task EvictPolicy_Should_Select_All_Terminal_Steps()
        {
            var state = CreateIndependentCompletedState(200);

            var policy = new EvictAiRetentionPolicy();

            var result = await policy.ExecuteAsync(
                new AiRetentionContext
                {
                    ExecutionState = state
                });

            var decision = GetDecision(result);

            Assert.Equal(AiRetentionDecisionKind.Evict, decision.Kind);

            Assert.NotNull(decision.StepsToEvict);
            Assert.Equal(200, decision.StepsToEvict.Count);

            Assert.Equal(200, state.Steps.Count);

            Assert.All(decision.StepsToEvict, stepName =>
            {
                var index = ExtractStepIndex(stepName);

                Assert.True(index >= 0);
            });
        }

        /// <summary>
        /// Validates that hybrid retention selects terminal steps for both compaction and eviction.
        /// </summary>
        [Fact]
        public async Task HybridPolicy_Should_Select_Steps_For_Compaction_And_Eviction()
        {
            var state = CreateBranchedState();

            var policy = new HybridAiRetentionPolicy();

            var result = await policy.ExecuteAsync(
                new AiRetentionContext
                {
                    ExecutionState = state
                });

            var decision = GetDecision(result);

            Assert.Equal(AiRetentionDecisionKind.Hybrid, decision.Kind);

            Assert.NotNull(decision.StepsToCompact);
            Assert.NotNull(decision.StepsToEvict);

            Assert.Equal(6, decision.StepsToCompact.Count);
            Assert.Equal(6, decision.StepsToEvict.Count);

            Assert.Contains("step-0", decision.StepsToCompact);
            Assert.Contains("step-5", decision.StepsToCompact);

            Assert.Contains("step-0", decision.StepsToEvict);
            Assert.Contains("step-5", decision.StepsToEvict);
        }

        /// <summary>
        /// Validates that non-terminal steps are excluded from retention decisions.
        /// </summary>
        [Fact]
        public async Task Policies_Should_Ignore_NonTerminal_Steps()
        {
            var state = CreateIndependentCompletedState(20);

            state.Steps["running-step"] = CreateStep(
                "running-step",
                AiStepExecutionStatus.Running,
                completedAtUtc: null);

            state.Steps["ready-step"] = CreateStep(
                "ready-step",
                AiStepExecutionStatus.Ready,
                completedAtUtc: null);

            state.Steps["retry-step"] = CreateStep(
                "retry-step",
                AiStepExecutionStatus.WaitingForRetry,
                completedAtUtc: null);

            var compactPolicy = new CompactAiRetentionPolicy();

            var compactResult = await compactPolicy.ExecuteAsync(
                new AiRetentionContext
                {
                    ExecutionState = state
                });

            var compactDecision = GetDecision(compactResult);

            Assert.Equal(20, compactDecision.StepsToCompact.Count);

            Assert.DoesNotContain("running-step", compactDecision.StepsToCompact);
            Assert.DoesNotContain("ready-step", compactDecision.StepsToCompact);
            Assert.DoesNotContain("retry-step", compactDecision.StepsToCompact);

            var evictPolicy = new EvictAiRetentionPolicy();

            var evictResult = await evictPolicy.ExecuteAsync(
                new AiRetentionContext
                {
                    ExecutionState = state
                });

            var evictDecision = GetDecision(evictResult);

            Assert.Equal(20, evictDecision.StepsToEvict.Count);

            Assert.DoesNotContain("running-step", evictDecision.StepsToEvict);
            Assert.DoesNotContain("ready-step", evictDecision.StepsToEvict);
            Assert.DoesNotContain("retry-step", evictDecision.StepsToEvict);
        }

        /// <summary>
        /// Extracts a strongly typed retention decision from a policy result.
        /// </summary>
        private static AiRetentionDecision GetDecision(
            AiPolicyResult result)
        {
            var typed = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result);

            Assert.NotNull(typed.Data);

            return typed.Data!;
        }

        /// <summary>
        /// Creates a linear completed state.
        /// </summary>
        private static AiExecutionState CreateLinearCompletedState(
            int stepCount)
        {
            var state = CreateEmptyState();

            for (var i = 0; i < stepCount; i++)
            {
                var stepName = $"step-{i}";

                var dependsOn = i == 0
                    ? new List<string>()
                    : new List<string>
                    {
                        $"step-{i - 1}"
                    };

                state.Steps[stepName] = CreateStep(
                    stepName,
                    AiStepExecutionStatus.Completed,
                    DateTime.UtcNow.AddSeconds(i),
                    dependsOn);
            }

            return state;
        }

        /// <summary>
        /// Creates a completed state with fully independent steps.
        /// </summary>
        private static AiExecutionState CreateIndependentCompletedState(
            int stepCount)
        {
            var state = CreateEmptyState();

            for (var i = 0; i < stepCount; i++)
            {
                var stepName = $"step-{i}";

                state.Steps[stepName] = CreateStep(
                    stepName,
                    AiStepExecutionStatus.Completed,
                    DateTime.UtcNow.AddSeconds(i),
                    new List<string>());
            }

            return state;
        }

        /// <summary>
        /// Creates a branched execution graph.
        /// </summary>
        private static AiExecutionState CreateBranchedState()
        {
            var state = CreateEmptyState();
            var now = DateTime.UtcNow;

            state.Steps["step-0"] = CreateStep(
                "step-0",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(0));

            state.Steps["step-1"] = CreateStep(
                "step-1",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(1));

            state.Steps["step-2"] = CreateStep(
                "step-2",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(2));

            state.Steps["step-3"] = CreateStep(
                "step-3",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(3),
                new List<string>
                {
                    "step-1",
                    "step-2"
                });

            state.Steps["step-4"] = CreateStep(
                "step-4",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(4),
                new List<string>
                {
                    "step-2"
                });

            state.Steps["step-5"] = CreateStep(
                "step-5",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(5),
                new List<string>
                {
                    "step-3"
                });

            return state;
        }

        /// <summary>
        /// Creates an empty execution state.
        /// </summary>
        private static AiExecutionState CreateEmptyState()
        {
            return new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                PipelineName = "retention-policy-test",
                Steps = new Dictionary<string, AiStepState>(StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Creates a step state.
        /// </summary>
        private static AiStepState CreateStep(
            string stepName,
            AiStepExecutionStatus status,
            DateTime? completedAtUtc,
            List<string>? dependsOn = null)
        {
            return new AiStepState
            {
                StepName = stepName,
                Status = status,
                CompletedAtUtc = completedAtUtc,
                DependsOn = dependsOn ?? new List<string>()
            };
        }

        /// <summary>
        /// Extracts the numeric step index from a step name.
        /// </summary>
        private static int ExtractStepIndex(
            string stepName)
        {
            var parts = stepName.Split('-');

            return int.Parse(parts[1]);
        }
    }
}
