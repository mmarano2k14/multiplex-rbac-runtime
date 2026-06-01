namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents the lifecycle status of an execution assistance lease.
    /// </summary>
    public enum AiExecutionAssistanceStatus
    {
        /// <summary>
        /// The assistance lease has been granted but has not started executing work yet.
        /// </summary>
        Granted = 0,

        /// <summary>
        /// The helper runtime instance is actively assisting the execution.
        /// </summary>
        Active = 1,

        /// <summary>
        /// The assistance lease has been released normally.
        /// </summary>
        Released = 2,

        /// <summary>
        /// The assistance lease expired before it was renewed or released.
        /// </summary>
        Expired = 3,

        /// <summary>
        /// The assistance lease was revoked by the controller.
        /// </summary>
        Revoked = 4,

        /// <summary>
        /// The assistance lease failed because the helper could not assist the execution.
        /// </summary>
        Failed = 5
    }
}