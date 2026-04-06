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
    /// - Step state is the single source of truth
    /// - Global execution status is a projection, not a stored truth
    /// - No implicit terminal states
    /// - Convergence must remain conservative under distributed uncertainty
    ///
    /// CONVERGENCE STATES:
    ///
    /// - Completed:
    ///   All steps are completed.
    ///
    /// - Running:
    ///   Work is actively executing OR immediately executable.
    ///
    /// - Waiting:
    ///   No work is executable now, but future progress is still possible.
    ///
    /// - Failed:
    ///   No further progress is possible and at least one step has failed.
    ///
    /// IMPORTANT DISTRIBUTED RULES:
    /// - Running is ONLY valid if lease is still active
    /// - Expired running work is considered recoverable (→ Waiting)
    /// - Retry-delayed work is non-terminal (→ Waiting)
    /// - Pending steps blocked by failed dependencies are NOT future work
    /// - Failure is only projected when ALL recovery paths are exhausted
    ///
    /// NOTE:
    /// This evaluator is NOT a reducer — it depends on runtime signals
    /// such as selector readiness and timing (retry / lease).
    /// </summary>
    public static class AiDagExecutionConvergenceEvaluator
    {
        /// <summary>
        /// Evaluates the global execution convergence state.
        ///
        /// ORDER MATTERS:
        /// 1. Completed (strongest terminal)
        /// 2. Running (active or immediately runnable)
        /// 3. Waiting (future possible)
        /// 4. Failed (terminal exhaustion)
        ///
        /// This ensures no premature terminal projection.
        /// </summary>
        public static AiDagExecutionConvergenceResult Evaluate(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);

            var resolvedSteps = pipeline.Steps
                .OrderBy(x => x.Order)
                .ToList();

            var stepStates = resolvedSteps
                .Select(x => state.GetOrCreateStep(x.Name))
                .ToList();

            if (stepStates.Count == 0)
            {
                return AiDagExecutionConvergenceResult.Waiting();
            }

            // Fast lookup
            var stepStateByName = stepStates.ToDictionary(x => x.StepName, StringComparer.Ordinal);

            // Selector determines what is executable NOW
            var readySteps = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state, utcNow);

            var allCompleted = stepStates.All(x => x.IsCompleted);

            // ------------------------------------------------------------
            // WORK THAT CAN RUN NOW
            // ------------------------------------------------------------
            var hasRunnableNow =
                readySteps.Count > 0 ||
                stepStates.Any(step =>
                    step.Status == AiStepExecutionStatus.WaitingForRetry &&
                    step.NextRetryAtUtc.HasValue &&
                    step.NextRetryAtUtc.Value <= utcNow);

            // ------------------------------------------------------------
            // ACTIVE RUNNING (LEASE VALID ONLY)
            // ------------------------------------------------------------
            var hasValidRunningLease = stepStates.Any(step =>
                step.Status == AiStepExecutionStatus.Running &&
                HasValidLease(step, utcNow));

            // ------------------------------------------------------------
            // FUTURE POSSIBILITY SIGNALS
            // ------------------------------------------------------------
            var hasFutureRetry = stepStates.Any(step =>
                step.Status == AiStepExecutionStatus.WaitingForRetry &&
                step.NextRetryAtUtc.HasValue &&
                step.NextRetryAtUtc.Value > utcNow);

            var hasRecoverableExpiredRunning = stepStates.Any(step =>
                step.Status == AiStepExecutionStatus.Running &&
                IsExpiredLease(step, utcNow));

            // ------------------------------------------------------------
            // PENDING STEPS THAT ARE STILL SALVAGEABLE
            // (not blocked by failed dependencies)
            // ------------------------------------------------------------
            var hasRecoverablePendingWork = resolvedSteps.Any(resolvedStep =>
            {
                var step = stepStateByName[resolvedStep.Name];

                if (step.Status != AiStepExecutionStatus.None)
                    return false;

                return CanStillBecomeRunnableLater(resolvedStep, stepStateByName, utcNow);
            });

            var hasFailedSteps = stepStates.Any(x => x.IsFailed);

            // ============================================================
            // COMPLETED
            // ============================================================
            if (allCompleted)
            {
                return AiDagExecutionConvergenceResult.Completed();
            }

            // ============================================================
            // RUNNING
            // ============================================================
            if (hasRunnableNow || hasValidRunningLease)
            {
                return AiDagExecutionConvergenceResult.Running();
            }

            // ============================================================
            // WAITING (future still possible)
            // ============================================================
            var canStillProgress =
                hasFutureRetry ||
                hasRecoverableExpiredRunning ||
                hasRecoverablePendingWork;

            if (canStillProgress)
            {
                return AiDagExecutionConvergenceResult.Waiting();
            }

            // ============================================================
            // FAILED (terminal)
            // ============================================================
            if (hasFailedSteps)
            {
                return AiDagExecutionConvergenceResult.Failed();
            }

            // ============================================================
            // SAFE FALLBACK
            // ============================================================
            return AiDagExecutionConvergenceResult.Waiting();
        }

        /// <summary>
        /// Determines if a running step is still protected by a valid lease.
        ///
        /// Only valid leases represent true in-flight work.
        /// Expired leases must NOT be treated as active execution.
        /// </summary>
        private static bool HasValidLease(AiStepState step, DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(step);

            return step.Status == AiStepExecutionStatus.Running
                && step.LeaseExpiresAtUtc.HasValue
                && step.LeaseExpiresAtUtc.Value > utcNow;
        }

        /// <summary>
        /// Determines if a running step has an expired lease.
        ///
        /// This represents stale work that can still be recovered.
        /// It must NOT be treated as active execution,
        /// but must still prevent premature failure.
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
        /// A step is salvageable only if ALL its dependencies still have a valid path forward.
        ///
        /// A dependency is considered viable if:
        /// - Completed
        /// - Running (valid or expired → recoverable)
        /// - WaitingForRetry
        /// - Ready
        /// - Not yet initialized (None)
        ///
        /// A dependency is terminally blocking if:
        /// - Failed
        ///
        /// IMPORTANT:
        /// This logic prevents falsely keeping execution in Waiting
        /// when a branch is already irrecoverably broken.
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
                    // Defensive guard: pipeline inconsistency
                    return false;
                }

                if (dependencyStep.IsCompleted)
                    continue;

                if (dependencyStep.IsFailed)
                    return false;

                if (dependencyStep.Status == AiStepExecutionStatus.WaitingForRetry)
                    continue;

                if (dependencyStep.Status == AiStepExecutionStatus.Ready)
                    continue;

                if (dependencyStep.Status == AiStepExecutionStatus.None)
                    continue;

                if (dependencyStep.Status == AiStepExecutionStatus.Running)
                {
                    // Running is valid if lease is active OR recoverable if expired
                    if (HasValidLease(dependencyStep, utcNow))
                        continue;

                    if (IsExpiredLease(dependencyStep, utcNow))
                        continue;
                }
            }

            return true;
        }
    }
}