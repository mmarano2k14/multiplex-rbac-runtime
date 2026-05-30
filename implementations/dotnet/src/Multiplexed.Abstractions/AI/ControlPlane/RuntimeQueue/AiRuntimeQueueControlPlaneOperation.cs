namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue
{
    /// <summary>
    /// Defines high-level local runtime queue control-plane operations.
    ///
    /// These operations control the local queue owned by one runtime instance.
    /// They do not represent the future shared/global queue.
    /// </summary>
    public enum AiRuntimeQueueControlPlaneOperation
    {
        /// <summary>
        /// Enqueues a new pipeline run into the local runtime queue.
        /// </summary>
        EnqueueRun = 0,

        /// <summary>
        /// Cancels a run by run id.
        /// </summary>
        CancelRun = 1,

        /// <summary>
        /// Cancels a run that is still queued locally.
        /// </summary>
        CancelQueuedRun = 2,

        /// <summary>
        /// Pauses the local runtime queue.
        /// </summary>
        PauseQueue = 3,

        /// <summary>
        /// Resumes the local runtime queue.
        /// </summary>
        ResumeQueue = 4,

        /// <summary>
        /// Gets the current visibility state of a runtime run.
        /// </summary>
        GetRunStatus = 5,

        /// <summary>
        /// Gets the current visibility state of the local runtime queue.
        /// </summary>
        GetQueueStatus = 6
    }
}