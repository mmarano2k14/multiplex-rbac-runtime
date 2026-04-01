using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Defines structured runtime logging for the AI execution engine.
    ///
    /// This logger is responsible for orchestration-level events such as:
    /// - execution lifecycle
    /// - step lifecycle
    /// - errors and exceptions
    /// - runtime diagnostics
    /// </summary>
    public interface IAiExecutionEngineLogger
    {
        // =========================
        // STRUCTURED EVENTS (EXISTING)
        // =========================

        void ExecutionCreated(AiExecutionRecord record);

        void ExecutionLoaded(AiExecutionRecord record);

        void ExecutionCompleted(AiExecutionRecord record);

        void ExecutionAlreadyCompleted(AiExecutionRecord record);

        void StepException(string executionId, string stepName, Exception exception);

        void StepFailed(string executionId, string stepName, string? error);

        void StepCompleted(AiExecutionRecord record, string stepName);

        // =========================
        // NEW — RUNTIME DIAGNOSTICS
        // =========================

        /// <summary>
        /// Emits a low-level informational log.
        /// Used for tracing execution flow (cleanup, transitions, etc.).
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