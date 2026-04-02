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
    /// - Deterministic
    /// - Engine-oriented
    /// - Aligned with scheduler readiness semantics
    ///
    /// IMPORTANT:
    /// This evaluator may depend on selector-based readiness checks.
    /// As a result, it should be treated as a runtime convergence evaluator
    /// rather than a strictly side-effect-free mathematical reducer.
    /// </summary>
    public static class AiDagExecutionConvergenceEvaluator
    {
        public static AiDagExecutionConvergenceResult Evaluate(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            DateTime utcNow)
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
            var hasReadySteps = AiPipelineDagStepSelector
                .SelectReadySteps(pipeline, state, utcNow)
                .Count > 0;

            if (allCompleted && !hasRunningSteps && !hasClaimedSteps)
            {
                return AiDagExecutionConvergenceResult.Completed();
            }

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
