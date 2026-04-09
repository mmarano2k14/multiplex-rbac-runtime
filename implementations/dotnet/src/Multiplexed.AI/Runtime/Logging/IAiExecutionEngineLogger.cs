using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Defines structured runtime logging for the AI execution engine.
    ///
    /// RESPONSIBILITIES:
    /// - execution lifecycle logging
    /// - step lifecycle logging
    /// - replay / recovery / retry diagnostics
    /// - distributed orchestration visibility
    /// - low-level runtime diagnostics
    ///
    /// DESIGN:
    /// - Structured methods should be preferred for important orchestration events
    /// - Generic LogInformation / LogWarning / LogError methods remain available
    ///   for detailed tracing and fallback diagnostics
    /// - Implementations may forward these events to logs, metrics, timelines,
    ///   realtime streams, or observability backends
    /// </summary>
    public interface IAiExecutionEngineLogger
    {
        // =========================
        // EXECUTION LIFECYCLE
        // =========================

        /// <summary>
        /// Logs that a new execution has been created.
        /// </summary>
        void ExecutionCreated(AiExecutionRecord record);

        /// <summary>
        /// Logs that an execution has been loaded from persistence.
        /// </summary>
        void ExecutionLoaded(AiExecutionRecord record);

        /// <summary>
        /// Logs that an execution reached a terminal state.
        /// </summary>
        void ExecutionCompleted(AiExecutionRecord record);

        /// <summary>
        /// Logs that a terminal execution was requested again.
        /// </summary>
        void ExecutionAlreadyCompleted(AiExecutionRecord record);

        // =========================
        // STEP LIFECYCLE
        // =========================

        /// <summary>
        /// Logs that a step completed successfully.
        /// </summary>
        void StepCompleted(AiExecutionRecord record, string stepName);

        /// <summary>
        /// Logs that a step failed with a business/runtime error result.
        /// </summary>
        void StepFailed(string executionId, string stepName, string? error);

        /// <summary>
        /// Logs that a step threw an exception during execution.
        /// </summary>
        void StepException(string executionId, string stepName, Exception exception);

        /// <summary>
        /// Logs that a distributed step was successfully claimed by a worker.
        /// </summary>
        void StepClaimed(string executionId, string stepName, string workerId, string claimToken);

        /// <summary>
        /// Logs that a step has been scheduled for retry.
        /// </summary>
        void StepRetryScheduled(string executionId, string stepName, int retryCount, DateTime? nextRetryAtUtc);

        /// <summary>
        /// Logs that one or more timed-out running steps have been recovered.
        /// </summary>
        void StepsRecovered(string executionId, int recoveredCount);

        // =========================
        // REPLAY / SNAPSHOT
        // =========================

        /// <summary>
        /// Logs that an execution was restored from a persisted snapshot.
        /// </summary>
        void ExecutionReplayRestored(string executionId, AiExecutionStatus status, int stepsCount);

        /// <summary>
        /// Logs that replay was skipped because a compatible execution
        /// already existed in the runtime store.
        /// </summary>
        void ExecutionReplaySkipped(string executionId, string reason);

        /// <summary>
        /// Logs that a terminal snapshot was persisted.
        /// </summary>
        void SnapshotPersisted(string executionId, AiExecutionStatus status);

        // =========================
        // FINALIZATION / CLEANUP
        // =========================

        /// <summary>
        /// Logs that distributed finalization succeeded.
        /// </summary>
        void FinalizationSucceeded(string executionId, AiExecutionStatus status);

        /// <summary>
        /// Logs that this worker lost the distributed finalization race.
        /// </summary>
        void FinalizationRaceLost(string executionId, AiExecutionStatus status);

        /// <summary>
        /// Logs that automatic cleanup is starting.
        /// </summary>
        void CleanupStarted(string executionId, AiExecutionStatus status);

        /// <summary>
        /// Logs that automatic cleanup completed successfully.
        /// </summary>
        void CleanupCompleted(string executionId);

        /// <summary>
        /// Logs that automatic cleanup was skipped.
        /// </summary>
        void CleanupSkipped(string executionId, string reason);

        // =========================
        // LOW-LEVEL DIAGNOSTICS
        // =========================

        /// <summary>
        /// Emits a low-level informational log.
        /// Used for tracing execution flow and detailed diagnostics.
        /// </summary>
        void LogInformation(string message);

        /// <summary>
        /// Emits a warning log.
        /// Used for recoverable or unexpected situations.
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Emits an error log with exception.
        /// Used for failures that impact execution flow.
        /// </summary>
        void LogError(Exception exception, string message);

        /// <summary>
        /// Emits an error log without exception.
        /// </summary>
        void LogError(string message);
    }
}