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
        /// <summary>
        /// Selects all steps that are currently eligible for execution.
        ///
        /// The returned list is ordered deterministically using the resolved pipeline order.
        /// </summary>
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

        /// <summary>
        /// Selects the next eligible step using deterministic pipeline order.
        /// </summary>
        public static ResolvedAiPipelineStep? SelectNextReadyStep(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            DateTime utcNow)
        {
            return SelectReadySteps(pipeline, state, utcNow).FirstOrDefault();
        }

        /// <summary>
        /// Determines whether all pipeline steps have completed successfully.
        /// </summary>
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

        /// <summary>
        /// Determines whether a step is currently eligible for execution.
        ///
        /// ELIGIBILITY RULES:
        /// - Completed steps are never eligible
        /// - Terminally failed steps are never eligible
        /// - Running steps are never eligible
        /// - WaitingForRetry steps are eligible only when their retry window is due
        /// - Non-schedulable statuses are excluded
        /// - All declared dependencies must already be completed
        /// </summary>
        private static bool IsStepEligible(
            AiStepState stepState,
            ResolvedAiPipelineStep step,
            AiExecutionState state,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(stepState);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(state);

            // Terminal success and terminal failure must never be selected again.
            if (stepState.Status == AiStepExecutionStatus.Completed ||
                stepState.Status == AiStepExecutionStatus.Failed)
            {
                return false;
            }

            // A running step is already owned or executing and cannot be selected again.
            if (stepState.Status == AiStepExecutionStatus.Running)
            {
                return false;
            }

            // Retry-waiting steps remain explicitly non-runnable until their retry window opens.
            // Once the retry time is due, the step is promoted back to Ready and may continue
            // through normal schedulability and dependency checks.
            if (stepState.Status == AiStepExecutionStatus.WaitingForRetry)
            {
                if (stepState.NextRetryAtUtc.HasValue &&
                    stepState.NextRetryAtUtc.Value > utcNow)
                {
                    return false;
                }

                stepState.PromoteRetryToReadyIfDue(utcNow);
            }

            // Only schedulable states may continue.
            // At this stage this typically means Ready or None.
            if (!stepState.IsSchedulable)
            {
                return false;
            }

            // Root nodes with no dependencies are immediately eligible once schedulable.
            if (step.DependsOn.Count == 0)
            {
                return true;
            }

            // DAG dependency rule:
            // all upstream dependencies must already be completed.
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