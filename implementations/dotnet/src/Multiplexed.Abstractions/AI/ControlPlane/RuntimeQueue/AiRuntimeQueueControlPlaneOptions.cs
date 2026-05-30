namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue
{
    /// <summary>
    /// Defines options for the local runtime queue control-plane facade.
    ///
    /// These options only control the high-level control-plane layer.
    /// They do not replace local queues, workers, runtime instances, or DAG execution logic.
    /// </summary>
    public sealed class AiRuntimeQueueControlPlaneOptions
    {
        /// <summary>
        /// Enables enqueueing new runs into the local runtime queue.
        /// </summary>
        public bool EnableEnqueueRun { get; init; } = true;

        /// <summary>
        /// Enables cancellation of runs by run id.
        /// </summary>
        public bool EnableCancelRun { get; init; } = true;

        /// <summary>
        /// Enables cancellation of runs that are still queued locally.
        /// </summary>
        public bool EnableCancelQueuedRun { get; init; } = true;

        /// <summary>
        /// Enables pausing the local runtime queue.
        /// </summary>
        public bool EnablePauseQueue { get; init; } = true;

        /// <summary>
        /// Enables resuming the local runtime queue.
        /// </summary>
        public bool EnableResumeQueue { get; init; } = true;

        /// <summary>
        /// Enables local runtime run status retrieval.
        /// </summary>
        public bool EnableGetRunStatus { get; init; } = true;

        /// <summary>
        /// Enables local runtime queue status retrieval.
        /// </summary>
        public bool EnableGetQueueStatus { get; init; } = true;

        /// <summary>
        /// When enabled, expected operational failures should be returned as
        /// structured failed results instead of being thrown to the caller.
        /// </summary>
        public bool ReturnFailureResultInsteadOfThrowing { get; init; } = true;

        /// <summary>
        /// Enables controller-level duration measurement.
        ///
        /// This is useful for future Grafana metrics and control-plane diagnostics.
        /// </summary>
        public bool MeasureDuration { get; init; } = true;
    }
}