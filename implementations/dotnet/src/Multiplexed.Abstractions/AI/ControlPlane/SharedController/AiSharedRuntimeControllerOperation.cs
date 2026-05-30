namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController
{
    /// <summary>
    /// Defines high-level shared runtime controller operations.
    /// </summary>
    public enum AiSharedRuntimeControllerOperation
    {
        /// <summary>
        /// Submits a new run to the shared runtime controller.
        /// </summary>
        SubmitRun = 0,

        /// <summary>
        /// Gets the status of a shared run known by the controller.
        /// </summary>
        GetRun = 1,

        /// <summary>
        /// Lists shared runs known by the controller.
        /// </summary>
        ListRuns = 2,

        /// <summary>
        /// Cancels a shared run before or after assignment.
        /// </summary>
        CancelRun = 3
    }
}