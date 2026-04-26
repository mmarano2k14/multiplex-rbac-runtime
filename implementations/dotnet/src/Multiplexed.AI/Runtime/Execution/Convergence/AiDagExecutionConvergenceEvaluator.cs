using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Pipeline;

namespace Multiplexed.AI.Runtime.Execution.Convergence
{
    /// <summary>
    /// Evaluates the global lifecycle convergence of a DAG execution
    /// from the resolved pipeline topology and the current durable step state.
    ///
    /// DESIGN PRINCIPLES:
    /// - Deterministic evaluation: identical inputs produce identical output.
    /// - Step state is the single source of truth.
    /// - Global execution status is a projection, not stored truth.
    /// - No implicit terminal states are inferred too early.
    /// - Convergence remains conservative under distributed uncertainty.
    ///
    /// IMPORTANT:
    /// - This evaluator does not mutate execution state directly.
    /// - Missing step states are initialized through <see cref="IAiExecutionStateWriter"/>.
    /// - This keeps <see cref="AiExecutionState"/> as a persistence model and routes
    ///   mutation through the writer.
    ///
    /// CONVERGENCE STATES:
    /// - Completed: all steps are completed.
    /// - Running: work is actively executing or immediately executable.
    /// - Waiting: no work is executable now, but future progress is still possible.
    /// - Failed: no further progress is possible and at least one step has failed.
    ///
    /// DISTRIBUTED RULES:
    /// - Running is only valid while the step lease is still active.
    /// - Expired running work is treated as recoverable and projects Waiting.
    /// - Retry-delayed work is non-terminal and projects Waiting.
    /// - Pending steps blocked by failed dependencies are not future work.
    /// - Failure is only projected when all recovery paths are exhausted.
    /// </summary>
    public static class AiDagExecutionConvergenceEvaluator
    {
        /// <summary>
        /// Evaluates the global execution convergence state.
        ///
        /// ORDER MATTERS:
        /// 1. Completed: strongest terminal state.
        /// 2. Running: active or immediately runnable work exists.
        /// 3. Waiting: no immediate work, but future progress is possible.
        /// 4. Failed: terminal exhaustion after recovery paths are gone.
        ///
        /// This ordering prevents premature terminal projection in distributed execution.
        /// </summary>
        public static AiDagExecutionConvergenceResult Evaluate(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);

            var resolvedSteps = pipeline.Steps
                .OrderBy(x => x.Order)
                .ToList();

            var stepStates = resolvedSteps
                .Select(x => stateWriter.GetOrCreateStep(state, x.Name))
                .ToList();

            if (stepStates.Count == 0)
            {
                return AiDagExecutionConvergenceResult.Waiting();
            }

            var stepStateByName = stepStates.ToDictionary(
                x => x.StepName,
                StringComparer.Ordinal);

            var readySteps = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                stateWriter,
                utcNow);

            var allCompleted = stepStates.All(x => x.IsCompleted);

            var hasRunnableNow =
                readySteps.Count > 0 ||
                stepStates.Any(step =>
                    step.Status == AiStepExecutionStatus.WaitingForRetry &&
                    step.NextRetryAtUtc.HasValue &&
                    step.NextRetryAtUtc.Value <= utcNow);

            var hasValidRunningLease = stepStates.Any(step =>
                step.Status == AiStepExecutionStatus.Running &&
                HasValidLease(step, utcNow));

            var hasFutureRetry = stepStates.Any(step =>
                step.Status == AiStepExecutionStatus.WaitingForRetry &&
                step.NextRetryAtUtc.HasValue &&
                step.NextRetryAtUtc.Value > utcNow);

            var hasRecoverableExpiredRunning = stepStates.Any(step =>
                step.Status == AiStepExecutionStatus.Running &&
                IsExpiredLease(step, utcNow));

            var hasRecoverablePendingWork = resolvedSteps.Any(resolvedStep =>
            {
                var step = stepStateByName[resolvedStep.Name];

                if (step.Status != AiStepExecutionStatus.None)
                {
                    return false;
                }

                return CanStillBecomeRunnableLater(
                    resolvedStep,
                    stepStateByName,
                    utcNow);
            });

            var hasFailedSteps = stepStates.Any(x => x.IsFailed);

            if (allCompleted)
            {
                return AiDagExecutionConvergenceResult.Completed();
            }

            if (hasRunnableNow || hasValidRunningLease)
            {
                return AiDagExecutionConvergenceResult.Running();
            }

            var canStillProgress =
                hasFutureRetry ||
                hasRecoverableExpiredRunning ||
                hasRecoverablePendingWork;

            if (canStillProgress)
            {
                return AiDagExecutionConvergenceResult.Waiting();
            }

            if (hasFailedSteps)
            {
                return AiDagExecutionConvergenceResult.Failed();
            }

            return AiDagExecutionConvergenceResult.Waiting();
        }

        /// <summary>
        /// Determines whether a running step still has a valid lease.
        ///
        /// RULE:
        /// - A running step is considered active only while its lease is valid.
        /// - Expired leases must not be treated as active execution.
        /// </summary>
        private static bool HasValidLease(AiStepState step, DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(step);

            return step.Status == AiStepExecutionStatus.Running
                && step.LeaseExpiresAtUtc.HasValue
                && step.LeaseExpiresAtUtc.Value > utcNow;
        }

        /// <summary>
        /// Determines whether a running step has an expired lease.
        ///
        /// RULE:
        /// - Expired running work is stale, not active.
        /// - It remains recoverable and must prevent premature failure projection.
        /// </summary>
        private static bool IsExpiredLease(AiStepState step, DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(step);

            return step.Status == AiStepExecutionStatus.Running
                && step.LeaseExpiresAtUtc.HasValue
                && step.LeaseExpiresAtUtc.Value <= utcNow;
        }

        /// <summary>
        /// Determines whether a pending step can still become runnable later.
        ///
        /// RULE:
        /// A step remains salvageable only if all dependencies still have a valid path forward.
        ///
        /// Dependency states considered viable:
        /// - Completed.
        /// - Running with a valid lease.
        /// - Running with an expired lease, because it can be recovered.
        /// - WaitingForRetry.
        /// - Ready.
        /// - None.
        ///
        /// Dependency states considered terminal blockers:
        /// - Failed.
        ///
        /// This prevents keeping execution in Waiting when a dependency chain is already
        /// irrecoverably broken.
        /// </summary>
        private static bool CanStillBecomeRunnableLater(
            ResolvedAiPipelineStep resolvedStep,
            IReadOnlyDictionary<string, AiStepState> stepStateByName,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(resolvedStep);
            ArgumentNullException.ThrowIfNull(stepStateByName);

            foreach (var dependencyName in resolvedStep.DependsOn)
            {
                if (!stepStateByName.TryGetValue(dependencyName, out var dependencyStep))
                {
                    return false;
                }

                if (dependencyStep.IsCompleted)
                {
                    continue;
                }

                if (dependencyStep.IsFailed)
                {
                    return false;
                }

                if (dependencyStep.Status == AiStepExecutionStatus.WaitingForRetry)
                {
                    continue;
                }

                if (dependencyStep.Status == AiStepExecutionStatus.Ready)
                {
                    continue;
                }

                if (dependencyStep.Status == AiStepExecutionStatus.None)
                {
                    continue;
                }

                if (dependencyStep.Status == AiStepExecutionStatus.Running)
                {
                    if (HasValidLease(dependencyStep, utcNow))
                    {
                        continue;
                    }

                    if (IsExpiredLease(dependencyStep, utcNow))
                    {
                        continue;
                    }

                    return false;
                }

                return false;
            }

            return true;
        }
    }
}