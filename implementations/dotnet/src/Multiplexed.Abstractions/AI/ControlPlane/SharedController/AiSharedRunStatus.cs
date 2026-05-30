namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController
{
    /// <summary>
    /// Defines the controller-level status of a shared runtime run.
    /// </summary>
    public enum AiSharedRunStatus
    {
        /// <summary>
        /// The shared run status is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The shared run was submitted to the shared controller.
        /// </summary>
        Submitted = 1,

        /// <summary>
        /// The shared run was accepted by admission.
        /// </summary>
        Accepted = 2,

        /// <summary>
        /// The shared run was assigned to a runtime instance.
        /// </summary>
        AssignedToInstance = 3,

        /// <summary>
        /// The shared run is waiting in a future shared/global queue.
        /// </summary>
        QueuedGlobally = 4,

        /// <summary>
        /// The shared run could not be assigned and scale-out was requested.
        /// </summary>
        ScaleOutRequested = 5,

        /// <summary>
        /// The shared run was rejected by admission.
        /// </summary>
        Rejected = 6,

        /// <summary>
        /// The shared run was cancelled before completion.
        /// </summary>
        Cancelled = 7,

        /// <summary>
        /// The shared run was dispatched to a local runtime queue.
        /// </summary>
        Dispatched = 8,

        /// <summary>
        /// The shared run completed.
        /// </summary>
        Completed = 9,

        /// <summary>
        /// The shared run failed.
        /// </summary>
        Failed = 10
    }
}