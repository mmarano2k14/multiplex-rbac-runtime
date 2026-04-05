using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Pipeline;

namespace Multiplexed.AI.Runtime.Execution.Convergence
{
    /// <summary>
    /// Evaluates the global lifecycle convergence of a DAG execution
    /// based on the resolved pipeline topology and current step state.
    ///
    /// DESIGN PRINCIPLES:
    /// - Deterministic evaluation (same input -> same output)
    /// - Step state is the source of truth
    /// - Global execution status is only a projection
    /// - No implicit terminal states
    ///
    /// CONVERGENCE RULES:
    /// - Completed:
    ///   All steps are completed and no step is still running or claimed.
    ///
    /// - Failed:
    ///   At least one step has failed and no further progress is possible.
    ///
    /// - Running:
    ///   Any step is currently running or immediately executable.
    ///
    /// - Waiting:
    ///   No work is currently running, but future progress is still possible
    ///   because the execution is waiting on retry timing, dependency completion,
    ///   or unresolved / uninitialized steps.
    ///
    /// IMPORTANT:
    /// This evaluator depends on selector-based readiness checks and is therefore
    /// considered a runtime convergence evaluator rather than a pure reducer.
    /// </summary>
    public static class AiDagExecutionConvergenceEvaluator
    {
        /// <summary>
        /// Evaluates the global execution convergence state of the pipeline.
        /// </summary>
        /// <param name="pipeline">The resolved pipeline definition.</param>
        /// <param name="state">The current execution state.</param>
        /// <param name="utcNow">Current UTC time used for retry and readiness evaluation.</param>
        /// <returns>A convergence result describing the global execution status.</returns>
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

            var hasRunningSteps = stepStates.Any(x => x.IsRunning);
            var hasClaimedSteps = stepStates.Any(HasActiveClaim);
            var hasFailedSteps = stepStates.Any(x => x.IsFailed);
            var hasWaitingForRetrySteps = stepStates.Any(x => x.IsWaitingForRetry);
            var hasPendingSteps = stepStates.Any(x => x.Status == AiStepExecutionStatus.None);
            var allCompleted = stepStates.All(x => x.IsCompleted);

            var readySteps = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state, utcNow);
            var hasReadySteps = readySteps.Count > 0;

            // -----------------------------------------------------------------
            // TERMINAL SUCCESS:
            // All steps are completed and no active claim still exists.
            // -----------------------------------------------------------------
            if (allCompleted && !hasRunningSteps && !hasClaimedSteps)
            {
                return AiDagExecutionConvergenceResult.Completed();
            }

            // -----------------------------------------------------------------
            // ACTIVE EXECUTION:
            // Work is either actively running or immediately executable now.
            // -----------------------------------------------------------------
            if (hasRunningSteps || hasReadySteps)
            {
                return AiDagExecutionConvergenceResult.Running();
            }

            // -----------------------------------------------------------------
            // WAITING FOR FUTURE PROGRESS:
            // Retry-delayed work is explicitly non-terminal.
            //
            // IMPORTANT:
            // Even if one or more steps have already failed, retry-waiting steps
            // must prevent terminal failure projection until their retry window
            // has either been consumed or all progress is truly exhausted.
            // -----------------------------------------------------------------
            if (hasWaitingForRetrySteps)
            {
                return AiDagExecutionConvergenceResult.Waiting();
            }

            // -----------------------------------------------------------------
            // PROGRESS POSSIBILITY CHECK:
            // Progress is still possible if:
            // - a step is currently claimed
            // - a step is still uninitialized / unresolved
            //
            // WaitingForRetry was already handled above and returned Waiting.
            // Ready and Running were already handled above and returned Running.
            // -----------------------------------------------------------------
            var canStillProgress =
                hasClaimedSteps ||
                hasPendingSteps;

            // -----------------------------------------------------------------
            // TERMINAL FAILURE:
            // At least one step has failed and no further progress remains possible.
            // -----------------------------------------------------------------
            if (hasFailedSteps && !canStillProgress)
            {
                return AiDagExecutionConvergenceResult.Failed();
            }

            // -----------------------------------------------------------------
            // DEFAULT:
            // No work is currently runnable, but the execution cannot yet be
            // considered terminal. This includes dependency wait or unresolved steps.
            // -----------------------------------------------------------------
            return AiDagExecutionConvergenceResult.Waiting();
        }

        /// <summary>
        /// Determines whether a step is currently claimed by a worker.
        ///
        /// A claimed step indicates in-flight or partially processed distributed work,
        /// even if the step is not currently visible as locally runnable.
        /// </summary>
        /// <param name="step">The step state.</param>
        /// <returns><c>true</c> if the step has active claim ownership; otherwise <c>false</c>.</returns>
        private static bool HasActiveClaim(AiStepState step)
        {
            ArgumentNullException.ThrowIfNull(step);

            return !string.IsNullOrWhiteSpace(step.ClaimedBy)
                || !string.IsNullOrWhiteSpace(step.ClaimToken)
                || step.ClaimedAtUtc.HasValue;
        }
    }
}