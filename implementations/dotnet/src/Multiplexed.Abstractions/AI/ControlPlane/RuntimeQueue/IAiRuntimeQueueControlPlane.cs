namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue
{
    /// <summary>
    /// Defines the high-level local runtime queue control-plane facade.
    ///
    /// This abstraction exposes local queue/run operations without replacing
    /// the local runtime queue, worker pool, runtime instance, or DAG execution logic.
    ///
    /// Intended future callers:
    /// - HTTP API
    /// - MCP server
    /// - CLI
    /// - Dashboard
    /// - Kubernetes control-plane pod
    ///
    /// Important:
    /// This abstraction controls the local runtime queue owned by one runtime instance.
    /// It does not represent the future shared/global queue.
    /// </summary>
    public interface IAiRuntimeQueueControlPlane
    {
        /// <summary>
        /// Executes a local runtime queue control-plane operation.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> ExecuteAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Enqueues a new pipeline run into the local runtime queue.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> EnqueueRunAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a run by run id.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> CancelRunAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a run that is still queued locally.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> CancelQueuedRunAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses the local runtime queue.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> PauseQueueAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes the local runtime queue.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> ResumeQueueAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current visibility state of a runtime run.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> GetRunStatusAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current visibility state of the local runtime queue.
        /// </summary>
        /// <param name="request">The local runtime queue control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The local runtime queue control-plane result.</returns>
        Task<AiRuntimeQueueControlPlaneResult> GetQueueStatusAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default);
    }
}