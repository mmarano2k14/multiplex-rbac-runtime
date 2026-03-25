using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Defines structured runtime logging for the high-level pipeline service entry point.
    ///
    /// This logger is responsible for request/response style events emitted by
    /// the in-memory pipeline service, before and after pipeline execution.
    /// </summary>
    public interface IAiPipelineServiceLogger
    {
        /// <summary>
        /// Emits a structured event when pipeline execution is requested.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        void ExecutionRequested(AiExecutionContext context);

        /// <summary>
        /// Emits a structured event when pipeline execution completes.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <param name="result">The final pipeline result.</param>
        void ExecutionCompleted(AiExecutionContext context, string? result);
    }
}