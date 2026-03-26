using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Defines structured runtime logging for the AI execution engine.
    ///
    /// This logger is responsible for orchestration-level events such as:
    /// - execution creation
    /// - step failures
    /// - step exceptions
    /// - step completion
    ///
    /// It is intentionally focused on persisted execution orchestration,
    /// not on step retry internals or pipeline service entry points.
    /// </summary>
    public interface IAiExecutionEngineLogger
    {
        /// <summary>
        /// Emits a structured event when a new execution is created.
        /// </summary>
        /// <param name="record">The created execution record.</param>
        void ExecutionCreated(AiExecutionRecord record);

        /// <summary>
        /// Emits a structured event when a new execution is loaded.
        /// </summary>
        /// <param name="record">The created execution record.</param>
        void ExecutionLoaded(AiExecutionRecord record);

        /// <summary>
        /// Emits a structured event when a new execution is completed.
        /// </summary>
        /// <param name="record">The created execution record.</param>
        void ExecutionCompleted(AiExecutionRecord record);

        /// <summary>
        /// Emits a structured event when a new execution is already completed.
        /// </summary>
        /// <param name="record">The created execution record.</param>
        void ExecutionAlreadyCompleted(AiExecutionRecord record);

        /// <summary>
        /// Emits a structured event when a step throws an exception.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        /// <param name="exception">The thrown exception.</param>
        void StepException(string executionId, string stepName, Exception exception);

        /// <summary>
        /// Emits a structured event when a step returns a failed result.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        /// <param name="error">The returned error message.</param>
        void StepFailed(string executionId, string stepName, string? error);

        /// <summary>
        /// Emits a structured event when a step completes successfully.
        /// </summary>
        /// <param name="record">The updated execution record.</param>
        /// <param name="step">The completed step.</param>
        void StepCompleted(AiExecutionRecord record, string stepName);


        
    }
}