namespace Multiplexed.Abstractions.AI.Execution.Control
{
    /// <summary>
    /// Represents the latest requested control action for an AI execution.
    /// </summary>
    /// <remarks>
    /// Actions describe intent, while <see cref="AiExecutionControlStatus"/> describes
    /// the effective durable control state observed by the runtime.
    /// </remarks>
    public enum AiExecutionControlAction
    {
        /// <summary>
        /// No control action has been requested.
        /// </summary>
        None = 0,

        /// <summary>
        /// Requests that the execution stop claiming new work and enter a paused state.
        /// </summary>
        Pause = 1,

        /// <summary>
        /// Requests that a paused or waiting execution resume execution.
        /// </summary>
        Resume = 2,

        /// <summary>
        /// Requests cooperative cancellation of the execution.
        /// </summary>
        Cancel = 3,

        /// <summary>
        /// Requests that the execution wait for external or human input.
        /// </summary>
        WaitForInput = 4,

        /// <summary>
        /// Indicates that external or human input has been submitted.
        /// </summary>
        SubmitInput = 5
    }
}