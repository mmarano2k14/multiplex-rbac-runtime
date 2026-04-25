using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Metrics;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.AI.Runtime.Execution.Metrics;
using Multiplexed.AI.Runtime.Execution.Retention;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retention
{
    /// <summary>
    /// Integration-style tests for graph-aware execution state retention.
    ///
    /// PURPOSE:
    /// - Validates the retention policy directly without engine/store noise.
    /// - Proves graph-aware behavior for linear, non-linear, retry, failed and disabled cases.
    /// - Ensures retention metrics remain coherent.
    /// </summary>
    public sealed class AiExecutionStateRetentionPolicyTests
    {
        [Fact]
        public void LinearGraph_Should_Not_Evict_When_Dependencies_Require_Full_Chain()
        {
            var state = CreateLinearCompletedState(stepCount: 200);
            var metrics = new InMemoryAiExecutionRetentionMetrics();

            var policy = CreatePolicy(maxCompletedSteps: 50, metrics);

            policy.Apply(state);

            var snapshot = metrics.Snapshot();

            Assert.Equal(200, snapshot.TotalStepsBefore);
            Assert.Equal(200, snapshot.TotalStepsAfter);
            Assert.Equal(0, snapshot.EvictedSteps);
            Assert.Equal(200, state.Steps.Count);
        }

        [Fact]
        public void IndependentGraph_Should_Evict_Old_Completed_Steps()
        {
            var state = CreateIndependentCompletedState(stepCount: 200);
            var metrics = new InMemoryAiExecutionRetentionMetrics();

            var policy = CreatePolicy(maxCompletedSteps: 50, metrics);

            policy.Apply(state);

            var snapshot = metrics.Snapshot();

            Assert.Equal(200, snapshot.TotalStepsBefore);
            Assert.Equal(50, snapshot.TotalStepsAfter);
            Assert.Equal(150, snapshot.EvictedSteps);
            Assert.Equal(50, state.Steps.Count);

            Assert.All(state.Steps.Keys, key =>
            {
                var index = ExtractStepIndex(key);
                Assert.True(index >= 150, $"Unexpected retained step '{key}'.");
            });
        }

        [Fact]
        public void BranchedGraph_Should_Protect_Transitive_Dependencies_Of_Retained_Window()
        {
            var state = CreateBranchedState();
            var metrics = new InMemoryAiExecutionRetentionMetrics();

            var policy = CreatePolicy(maxCompletedSteps: 2, metrics);

            policy.Apply(state);

            var snapshot = metrics.Snapshot();

            Assert.Equal(6, snapshot.TotalStepsBefore);

            // step-4 and step-5 are retained by window.
            // step-5 depends on step-3, which depends on step-1 and step-2.
            // step-4 depends on step-2.
            Assert.Contains("step-1", state.Steps.Keys);
            Assert.Contains("step-2", state.Steps.Keys);
            Assert.Contains("step-3", state.Steps.Keys);
            Assert.Contains("step-4", state.Steps.Keys);
            Assert.Contains("step-5", state.Steps.Keys);

            // step-0 is old and not required by the retained subgraph.
            Assert.DoesNotContain("step-0", state.Steps.Keys);

            Assert.Equal(1, snapshot.EvictedSteps);
        }

        [Fact]
        public void Retention_Should_Not_Remove_NonTerminal_Steps()
        {
            var state = CreateIndependentCompletedState(stepCount: 20);

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

            state.Steps["failed-step"] = CreateStep(
                "failed-step",
                AiStepExecutionStatus.Failed,
                completedAtUtc: null);

            var metrics = new InMemoryAiExecutionRetentionMetrics();
            var policy = CreatePolicy(maxCompletedSteps: 5, metrics);

            policy.Apply(state);

            Assert.Contains("running-step", state.Steps.Keys);
            Assert.Contains("ready-step", state.Steps.Keys);
            Assert.Contains("retry-step", state.Steps.Keys);
            Assert.Contains("failed-step", state.Steps.Keys);

            var snapshot = metrics.Snapshot();

            Assert.Equal(24, snapshot.TotalStepsBefore);
            Assert.Equal(9, snapshot.TotalStepsAfter);
            Assert.Equal(15, snapshot.EvictedSteps);
            Assert.Equal(1, snapshot.ActiveSteps);
            Assert.Equal(2, snapshot.PendingSteps);
        }

        [Fact]
        public void DisabledRetention_Should_Not_Modify_State_Or_Record_Metrics()
        {
            var state = CreateIndependentCompletedState(stepCount: 200);
            var metrics = new InMemoryAiExecutionRetentionMetrics();

            var policy = new DefaultAiExecutionStateRetentionPolicy(
                new AiExecutionStateRetentionOptions
                {
                    Enabled = false,
                    MaxCompletedStepsInState = 50
                },
                metrics);

            policy.Apply(state);

            var snapshot = metrics.Snapshot();

            Assert.Equal(200, state.Steps.Count);
            Assert.Equal(0, snapshot.TotalStepsBefore);
            Assert.Equal(0, snapshot.TotalStepsAfter);
            Assert.Equal(0, snapshot.EvictedSteps);
        }

        private static DefaultAiExecutionStateRetentionPolicy CreatePolicy(
            int maxCompletedSteps,
            IAiExecutionRetentionMetrics metrics)
        {
            return new DefaultAiExecutionStateRetentionPolicy(
                new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    MaxCompletedStepsInState = maxCompletedSteps
                },
                metrics);
        }

        private static AiExecutionState CreateLinearCompletedState(int stepCount)
        {
            var state = CreateEmptyState();

            for (var i = 0; i < stepCount; i++)
            {
                var stepName = $"step-{i}";
                var dependsOn = i == 0
                    ? new List<string>()
                    : new List<string> { $"step-{i - 1}" };

                state.Steps[stepName] = CreateStep(
                    stepName,
                    AiStepExecutionStatus.Completed,
                    DateTime.UtcNow.AddSeconds(i),
                    dependsOn);
            }

            return state;
        }

        private static AiExecutionState CreateIndependentCompletedState(int stepCount)
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
                new List<string> { "step-1", "step-2" });

            state.Steps["step-4"] = CreateStep(
                "step-4",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(4),
                new List<string> { "step-2" });

            state.Steps["step-5"] = CreateStep(
                "step-5",
                AiStepExecutionStatus.Completed,
                now.AddSeconds(5),
                new List<string> { "step-3" });

            return state;
        }

        private static AiExecutionState CreateEmptyState()
        {
            return new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                PipelineName = "retention-policy-test",
                Steps = new Dictionary<string, AiStepState>(StringComparer.Ordinal)
            };
        }

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

        private static int ExtractStepIndex(string stepName)
        {
            var parts = stepName.Split('-');
            return int.Parse(parts[1]);
        }
    }
}