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
    /// - Missing hot step states are initialized through IAiExecutionStateWriter.
    /// - Archived / evicted step states can be resolved through IAiExecutionStepResolver.
    /// - This keeps AiExecutionState as hot state while preserving correctness after retention.
    /// </summary>
    public static class AiDagExecutionConvergenceEvaluator
    {
        /// <summary>
        /// Evaluates convergence using hot state only.
        ///
        /// IMPORTANT:
        /// - This method is kept for backward compatibility.
        /// - Once eviction is enabled, prefer EvaluateAsync with IAiExecutionStepResolver.
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

            return EvaluateResolvedStepStates(
                pipeline,
                state,
                stateWriter,
                resolvedSteps,
                stepStates,
                utcNow);
        }

        /// <summary>
        /// Evaluates convergence using hot state plus archived / evicted step payloads.
        ///
        /// PURPOSE:
        /// - Preserve convergence correctness after retention eviction.
        /// - Resolve evicted completed / failed steps from the external step index.
        /// - Keep all existing retry, lease, recovery, and pending-work rules unchanged.
        ///
        /// IMPORTANT:
        /// - This method must not simplify convergence rules.
        /// - It only changes how step states are loaded.
        /// - Missing steps are still initialized through the state writer.
        /// </summary>
        public static async Task<AiDagExecutionConvergenceResult> EvaluateAsync(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            IAiExecutionStepResolver stepResolver,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);
            ArgumentNullException.ThrowIfNull(stepResolver);

            var resolvedSteps = pipeline.Steps
                .OrderBy(x => x.Order)
                .ToList();

            var stepStates = new List<AiStepState>(resolvedSteps.Count);

            foreach (var resolvedStep in resolvedSteps)
            {
                var step = await stepResolver.GetStepAsync(
                        state.ExecutionId,
                        resolvedStep.Name,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                step ??= stateWriter.GetOrCreateStep(state, resolvedStep.Name);

                stepStates.Add(step);
            }

            return EvaluateResolvedStepStates(
                pipeline,
                state,
                stateWriter,
                resolvedSteps,
                stepStates,
                utcNow);
        }

        /// <summary>
        /// Evaluates convergence from already resolved step states.
        ///
        /// PURPOSE:
        /// - Keeps the original convergence algorithm in one place.
        /// - Allows both hot-state and resolver-aware paths to share identical logic.
        /// - Prevents accidental loss of retry / lease / recovery behavior.
        /// </summary>
        private static AiDagExecutionConvergenceResult EvaluateResolvedStepStates(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            IReadOnlyList<ResolvedAiPipelineStep> resolvedSteps,
            IReadOnlyList<AiStepState> stepStates,
            DateTime utcNow)
        {
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

        private static bool HasValidLease(AiStepState step, DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(step);

            return step.Status == AiStepExecutionStatus.Running
                && step.LeaseExpiresAtUtc.HasValue
                && step.LeaseExpiresAtUtc.Value > utcNow;
        }

        private static bool IsExpiredLease(AiStepState step, DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(step);

            return step.Status == AiStepExecutionStatus.Running
                && step.LeaseExpiresAtUtc.HasValue
                && step.LeaseExpiresAtUtc.Value <= utcNow;
        }

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