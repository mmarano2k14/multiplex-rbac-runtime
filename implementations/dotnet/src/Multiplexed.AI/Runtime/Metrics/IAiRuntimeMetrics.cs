using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.Metrics
{
    /// <summary>
    /// Defines runtime-level metrics for AI execution observability.
    ///
    /// This interface is intentionally simple and in-memory:
    /// - Used by Engine and Store to emit metrics
    /// - Can later be backed by Prometheus / OpenTelemetry without changing callers
    ///
    /// IMPORTANT:
    /// These metrics are for observability only.
    /// They must NEVER drive business logic decisions.
    /// </summary>
    public interface IAiRuntimeMetrics
    {
        /// <summary>
        /// Increments retry counter for a specific step.
        /// Called when a step transitions to WaitingForRetry.
        /// </summary>
        void IncrementRetry(string stepName);

        /// <summary>
        /// Increments recovery counter for a given execution.
        /// Called when timed-out steps are recovered by the store.
        /// </summary>
        void IncrementRecovery(string executionId, int recoveredCount);

        /// <summary>
        /// Increments the number of finalization attempts.
        /// Called before attempting atomic DAG finalization.
        /// </summary>
        void IncrementFinalizeAttempt();

        /// <summary>
        /// Increments successful finalizations.
        /// Called only when finalization succeeds.
        /// </summary>
        void IncrementFinalizeSuccess();

        /// <summary>
        /// Increments successful claim for a step.
        /// Called when a worker successfully claims a ready step.
        /// </summary>
        void IncrementClaimSuccess(string stepName);

        /// <summary>
        /// Increments claim miss (no step available or lost race).
        /// Useful to detect contention or idle workers.
        /// </summary>
        void IncrementClaimMiss();

        // ===== READ API (for debug / observability) =====

        long GetFinalizeAttempts();
        long GetFinalizeSuccess();
        long GetClaimMiss();

        IReadOnlyDictionary<string, long> GetRetryByStep();
        IReadOnlyDictionary<string, long> GetClaimSuccessByStep();
        IReadOnlyDictionary<string, long> GetRecoveryByExecution();
    }
}