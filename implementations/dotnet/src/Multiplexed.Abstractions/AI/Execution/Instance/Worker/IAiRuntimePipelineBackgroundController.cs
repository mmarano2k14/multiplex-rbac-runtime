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
    }
}