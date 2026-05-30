namespace Multiplexed.Abstractions.AI.ControlPlane.Observability
{
    /// <summary>
    /// Represents the outcome of a control-plane operation.
    /// </summary>
    public enum AiControlPlaneOperationOutcome
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Succeeded = 0,

        /// <summary>
        /// The operation completed but was denied by policy, configuration, or runtime state.
        /// </summary>
        Denied = 1,

        /// <summary>
        /// The operation failed because of an exception or unexpected runtime error.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// The operation completed with validation or diagnostic warnings.
        /// </summary>
        CompletedWithIssues = 3
    }
}