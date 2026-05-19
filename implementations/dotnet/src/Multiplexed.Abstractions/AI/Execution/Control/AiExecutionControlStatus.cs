namespace Multiplexed.Abstractions.AI.Execution.Control
{
    /// <summary>
    /// Represents the durable control status of an AI execution.
    /// </summary>
    /// <remarks>
    /// This status is separate from the DAG execution state. It describes operator,
    /// system, or user-level control over an execution, such as pausing, cancelling,
    /// or waiting for external input.
    /// </remarks>
    public enum AiExecutionControlStatus
    {
        /// <summary>
        /// No control state has been applied to the execution.
        /// </summary>
        None = 0,

        /// <summary>
        /// The execution is allowed to continue normally.
        /// </summary>
        Running = 1,

        /// <summary>
        /// A pause has been requested and workers should stop claiming new work.
        /// Already claimed work may finish safely.
        /// </summary>
        Pausing = 2,

        /// <summary>
        /// The execution is paused and should not advance until resumed.
        /// </summary>
        Paused = 3,

        /// <summary>
        /// A resume has been requested and the execution may become claimable again.
        /// </summary>
        Resuming = 4,

        /// <summary>
        /// Cancellation has been requested and workers should stop claiming new work.
        /// </summary>
        Cancelling = 5,

        /// <summary>
        /// The execution has been cancelled.
        /// </summary>
        Cancelled = 6,

        /// <summary>
        /// The execution is waiting for external or human input before it can continue.
        /// </summary>
        WaitingForInput = 7
    }
}