using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Pipeline;

namespace Multiplexed.AI.Runtime.Execution.Convergence
{
    /// <summary>
    /// Evaluates the global lifecycle convergence of a DAG execution
    /// from resolved pipeline topology and current step state.
    ///
    /// DESIGN:
    /// - Pure
    /// - Deterministic
    /// - No side effects
    ///
    /// This evaluator is the single place where global DAG lifecycle
    /// semantics are decided.
    /// </summary>
    public static class AiDagExecutionConvergenceEvaluator
    {
        /// <summary>
        /// Evaluates the current global DAG lifecycle status from pipeline topology
        /// and step runtime state.
        ///
        /// SEMANTICS:
        /// - Completed: all steps completed and no in-flight claims remain
        /// - Failed: one or more steps failed and no in-flight claims remain
        /// - Running: runnable work exists now, or work is currently executing
        /// - Waiting: no runnable work exists now, execution is not terminal,
        ///   and no local progress can currently be made
        ///
        /// CURRENT FAILURE POLICY:
        /// - Strict mode
        /// - Any failed step is considered globally fatal once the DAG converges
        /// </summary>
        public static AiDagExecutionConvergenceResult Evaluate(
            ResolvedAiPipeline pipeline,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);

            var stepStates = pipeline.Steps
                .OrderBy(x => x.Order)
                .Select(x => state.GetOrCreateStep(x.Name))
                .ToList();

            if (stepStates.Count == 0)
            {
                return AiDagExecutionConvergenceResult.Waiting();
            }

            var allCompleted = stepStates.All(x => x.IsCompleted);
            var hasFailedSteps = stepStates.Any(x => x.IsFailed);
            var hasRunningSteps = stepStates.Any(x => x.IsRunning);
            var hasClaimedSteps = stepStates.Any(HasActiveClaim);
            var hasReadySteps = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state).Count > 0;

            if (allCompleted && !hasRunningSteps && !hasClaimedSteps)
            {
                return AiDagExecutionConvergenceResult.Completed();
            }

            // Strict failure policy:
            // any failed step converges the DAG to Failed,
            // but only after all in-flight work has settled.
            if (hasFailedSteps && !hasRunningSteps && !hasClaimedSteps)
            {
                return AiDagExecutionConvergenceResult.Failed();
            }

            if (hasRunningSteps || hasReadySteps)
            {
                return AiDagExecutionConvergenceResult.Running();
            }

            return AiDagExecutionConvergenceResult.Waiting();
        }

        private static bool HasActiveClaim(AiStepState step)
        {
            ArgumentNullException.ThrowIfNull(step);

            return !string.IsNullOrWhiteSpace(step.ClaimedBy)
                || !string.IsNullOrWhiteSpace(step.ClaimToken)
                || step.ClaimedAtUtc.HasValue;
        }
    }
}
