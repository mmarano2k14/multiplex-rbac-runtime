using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Metrics.Execution
{
    /// <summary>
    /// Records metrics for AI execution lifecycle events.
    ///
    /// PURPOSE:
    /// - Provide observability over execution lifecycle.
    /// - Track step execution behavior across distributed workers.
    /// - Monitor retry, recovery, claim, and deterministic finalization behavior.
    ///
    /// IMPORTANT:
    /// - This interface is observational only.
    /// - It must not influence execution logic or state transitions.
    /// </summary>
    public interface IAiExecutionMetrics
    {
        /// <summary>
        /// Records that a new execution has started.
        /// </summary>
        void RecordExecutionStarted(string executionId);

        /// <summary>
        /// Records that an execution has completed successfully.
        /// </summary>
        void RecordExecutionCompleted(string executionId);

        /// <summary>
        /// Records that an execution has failed.
        /// </summary>
        void RecordExecutionFailed(string executionId);

        /// <summary>
        /// Records that a step has been claimed by a worker.
        /// </summary>
        void RecordStepClaimed(string executionId, string stepId);

        /// <summary>
        /// Records that no executable step could be claimed by a worker.
        /// </summary>
        void RecordStepClaimMiss(string executionId);

        /// <summary>
        /// Records that a step has completed successfully.
        /// </summary>
        void RecordStepCompleted(string executionId, string stepId);

        /// <summary>
        /// Records that a step execution has failed.
        /// </summary>
        void RecordStepFailed(string executionId, string stepId);

        /// <summary>
        /// Records that a step is being retried.
        /// </summary>
        void RecordStepRetried(string executionId, string stepId);

        /// <summary>
        /// Records that a step has been recovered after a timeout or worker crash.
        /// </summary>
        void RecordStepRecovered(string executionId, string stepId);

        /// <summary>
        /// Records that one or more steps were recovered for an execution.
        /// </summary>
        void RecordStepsRecovered(string executionId, int recoveredCount);

        /// <summary>
        /// Records that a finalization attempt was made.
        /// </summary>
        void RecordFinalizeAttempt(string executionId);

        /// <summary>
        /// Records that execution finalization succeeded.
        /// </summary>
        void RecordFinalizeSuccess(string executionId);

        /// <summary>
        /// Records that execution finalization encountered a conflict.
        /// </summary>
        void RecordFinalizeConflict(string executionId);

        /// <summary>
        /// Gets a snapshot of retry counts grouped by step identifier.
        /// </summary>
        IReadOnlyDictionary<string, long> GetRetryByStep();

        /// <summary>
        /// Gets a snapshot of successful claim counts grouped by step identifier.
        /// </summary>
        IReadOnlyDictionary<string, long> GetClaimSuccessByStep();

        /// <summary>
        /// Gets a snapshot of recovered step counts grouped by execution identifier.
        /// </summary>
        IReadOnlyDictionary<string, long> GetRecoveryByExecution();
    }
}