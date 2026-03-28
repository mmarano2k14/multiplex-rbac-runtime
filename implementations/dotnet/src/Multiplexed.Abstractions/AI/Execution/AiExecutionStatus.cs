namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the global orchestration lifecycle of an AI execution.
    ///
    /// This status answers the question:
    /// "What is the overall state of the execution?"
    ///
    /// This enum must remain small, stable, and focused on the execution as a whole.
    /// It must not be used to describe local step activity, retry mechanics,
    /// or error classification.
    /// </summary>
    public enum AiExecutionStatus
    {
        /// <summary>
        /// The execution has been created but has not started processing yet.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// The execution is actively processing.
        /// </summary>
        Running = 1,

        /// <summary>
        /// The execution is globally paused and waiting for an external event,
        /// callback, approval, or resumable condition.
        /// </summary>
        Waiting = 2,

        /// <summary>
        /// The execution completed successfully and is terminal.
        /// </summary>
        Completed = 3,

        /// <summary>
        /// The execution ended in a terminal failure state.
        /// </summary>
        Failed = 4,

        /// <summary>
        /// The execution was cancelled intentionally and is terminal.
        /// </summary>
        Cancelled = 5
    }
}