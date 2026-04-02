using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Provides helper methods for DAG-based step selection within an AI pipeline.
    ///
    /// This selector determines which step instances are ready for execution
    /// based on dependency satisfaction and per-step runtime status.
    ///
    /// DESIGN:
    /// - Deterministic
    /// - Stateless selector logic
    /// - Retry-aware
    ///
    /// IMPORTANT:
    /// This class does not execute steps.
    /// It only evaluates execution readiness from resolved topology and mutable step state.
    ///
    /// NOTE:
    /// This selector may promote a retry-waiting step back to Ready
    /// when its retry window becomes eligible.
    ///
    /// In distributed mode, this selector is only a local readiness helper.
    /// Final multi-worker safety must still be enforced by the distributed claim layer
    /// (for example Redis Lua).
    /// </summary>
    public static class AiPipelineDagStepSelector
    {
        public static IReadOnlyList<ResolvedAiPipelineStep> SelectReadySteps(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);

            var readySteps = new List<ResolvedAiPipelineStep>();

            foreach (var step in pipeline.Steps.OrderBy(x => x.Order))
            {
                var stepState = state.GetOrCreateStep(step.Name);

                if (!IsStepEligible(stepState, step, state, utcNow))
                {
                    continue;
                }

                readySteps.Add(step);
            }

            return readySteps;
        }

        public static ResolvedAiPipelineStep? SelectNextReadyStep(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            DateTime utcNow)
        {
            return SelectReadySteps(pipeline, state, utcNow).FirstOrDefault();
        }

        public static bool IsCompleted(
            ResolvedAiPipeline pipeline,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);

            foreach (var step in pipeline.Steps)
            {
                var stepState = state.GetOrCreateStep(step.Name);

                if (!stepState.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStepEligible(
            AiStepState stepState,
            ResolvedAiPipelineStep step,
            AiExecutionState state,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(stepState);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(state);

            if (stepState.Status == AiStepExecutionStatus.Completed ||
                stepState.Status == AiStepExecutionStatus.Failed)
            {
                return false;
            }

            if (stepState.Status == AiStepExecutionStatus.Running)
            {
                return false;
            }

            if (stepState.Status == AiStepExecutionStatus.WaitingForRetry)
            {
                if (stepState.NextRetryAtUtc.HasValue &&
                    stepState.NextRetryAtUtc.Value > utcNow)
                {
                    return false;
                }

                stepState.PromoteRetryToReadyIfDue(utcNow);
            }

            if (!stepState.IsSchedulable)
            {
                return false;
            }

            if (step.DependsOn.Count == 0)
            {
                return true;
            }

            foreach (var dependencyName in step.DependsOn)
            {
                var dependencyState = state.GetOrCreateStep(dependencyName);

                if (!dependencyState.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
