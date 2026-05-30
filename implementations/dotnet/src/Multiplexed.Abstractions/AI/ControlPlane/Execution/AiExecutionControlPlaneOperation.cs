namespace Multiplexed.Abstractions.AI.ControlPlane.Execution
{
    /// <summary>
    /// Defines high-level execution control-plane operations.
    /// </summary>
    public enum AiExecutionControlPlaneOperation
    {
        /// <summary>
        /// Requests that an execution stop claiming new work and move toward a paused state.
        /// </summary>
        Pause = 0,

        /// <summary>
        /// Requests that a paused, pausing, waiting, or resuming execution continue execution.
        /// </summary>
        Resume = 1,

        /// <summary>
        /// Requests cooperative cancellation of an execution.
        /// </summary>
        Cancel = 2,

        /// <summary>
        /// Submits external or human input for an execution waiting for input.
        /// </summary>
        SubmitHumanInput = 3,

        /// <summary>
        /// Gets the current operational control decision/status for an execution.
        /// </summary>
        GetStatus = 4
    }
}