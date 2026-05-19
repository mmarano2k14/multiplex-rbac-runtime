namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Controls background execution of submitted runtime pipeline runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controller owns a queue of submitted pipeline run requests and executes
    /// them in the background while respecting a configured maximum number of active
    /// executions.
    /// </para>
    /// <para>
    /// Each submitted request creates a new runtime execution and therefore a distinct
    /// execution identifier. Execution identifiers must not be reused across runs.
    /// </para>
    /// <para>
    /// This abstraction is the foundation for future pause, resume, cancel, and replay
    /// control-plane behavior.
    /// </para>
    /// </remarks>
    public interface IAiRuntimePipelineBackgroundController
    {
        /// <summary>
        /// Starts the background controller loop.
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        Task StartAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the background controller loop.
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        Task StopAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Enqueues one pipeline run request for background execution.
        /// </summary>
        /// <param name="request">
        /// The pipeline run request.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A handle that can be used to observe the submitted run status and completion.
        /// </returns>
        ValueTask<AiRuntimeWorkerRunHandle> EnqueueAsync(
            AiRuntimePipelineRunRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses the controller queue so accepted runs remain queued and no new queued run starts.
        /// </summary>
        /// <param name="reason">
        /// The optional reason for pausing the queue.
        /// </param>
        /// <param name="requestedBy">
        /// The optional identity requesting the queue pause.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous pause operation.
        /// </returns>
        /// <remarks>
        /// Pausing the controller queue does not pause already running executions. It only prevents
        /// queued runs from being started by the background controller until the queue is resumed.
        /// </remarks>
        Task PauseQueueAsync(
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes the controller queue so queued runs may start again.
        /// </summary>
        /// <param name="requestedBy">
        /// The optional identity requesting the queue resume.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous resume operation.
        /// </returns>
        Task ResumeQueueAsync(
            string? requestedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to cancel a queued run before it starts execution.
        /// </summary>
        /// <param name="runId">
        /// The controller run identifier.
        /// </param>
        /// <param name="reason">
        /// The optional cancellation reason.
        /// </param>
        /// <param name="requestedBy">
        /// The optional identity requesting cancellation.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// <c>true</c> if the queued run was cancelled; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This operation targets controller-level queued runs. If the run has already started
        /// and has a durable execution identifier, execution-level cancellation should be handled
        /// through the execution control service.
        /// </remarks>
        Task<bool> CancelQueuedRunAsync(
            string runId,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);
    }
}