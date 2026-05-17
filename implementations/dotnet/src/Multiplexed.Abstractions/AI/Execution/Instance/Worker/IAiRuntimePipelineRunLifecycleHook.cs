namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Defines lifecycle hooks for background-controller pipeline runs.
    /// </summary>
    public interface IAiRuntimePipelineRunLifecycleHook
    {
        /// <summary>
        /// Invoked after a background-controller run reaches a terminal state and
        /// terminal lifecycle processing has completed.
        /// </summary>
        /// <param name="context">The finalized run context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous hook operation.</returns>
        Task OnFinalizedAsync(
            AiRuntimePipelineRunFinalizedContext context,
            CancellationToken cancellationToken);
    }
}