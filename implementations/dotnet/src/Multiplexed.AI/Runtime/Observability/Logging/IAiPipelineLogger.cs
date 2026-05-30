using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Observability.Logging
{
    /// <summary>
    /// Defines structured runtime logging for sequential pipeline execution.
    ///
    /// This logger is responsible for events emitted by the in-memory step runner,
    /// including:
    /// - pipeline start/completion
    /// - step start/completion
    /// - step failure/exception
    /// - per-step duration
    /// </summary>
    public interface IAiPipelineLogger
    {
        /// <summary>
        /// Emits a structured event when pipeline execution starts.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepCount">The total number of registered steps.</param>
        void ExecutionStarted(string executionId, int stepCount);

        /// <summary>
        /// Emits a structured event when pipeline execution completes.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepCount">The total number of registered steps.</param>
        void ExecutionCompleted(string executionId, int stepCount);

        /// <summary>
        /// Emits a structured event when a step starts.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <param name="step">The step being executed.</param>
        void StepStarted(AiExecutionContext context, IAiStep step);

        /// <summary>
        /// Emits a structured event when a step throws an exception.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <param name="step">The step that threw the exception.</param>
        /// <param name="durationMs">The elapsed execution duration in milliseconds.</param>
        /// <param name="exception">The thrown exception.</param>
        void StepException(AiExecutionContext context, IAiStep step, long durationMs, Exception exception);

        /// <summary>
        /// Emits a structured event when a step returns a failed result.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <param name="step">The failed step.</param>
        /// <param name="durationMs">The elapsed execution duration in milliseconds.</param>
        /// <param name="result">The failed step result.</param>
        void StepFailed(AiExecutionContext context, IAiStep step, long durationMs, AiStepResult result);

        /// <summary>
        /// Emits a structured event when a step completes successfully.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <param name="step">The completed step.</param>
        /// <param name="durationMs">The elapsed execution duration in milliseconds.</param>
        /// <param name="result">The successful step result.</param>
        void StepCompleted(AiExecutionContext context, IAiStep step, long durationMs, AiStepResult result);
    }
}