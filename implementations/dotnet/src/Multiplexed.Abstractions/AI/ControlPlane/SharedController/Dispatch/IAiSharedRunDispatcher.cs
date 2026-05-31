namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController.Dispatch
{
    /// <summary>
    /// Defines a dispatcher that sends shared runs to runtime instance queues.
    /// </summary>
    /// <remarks>
    /// The dispatcher is responsible for bridging the shared controller layer
    /// to a selected runtime instance.
    ///
    /// Important:
    /// The dispatcher does not decide admission.
    /// It does not own the shared queue.
    /// It does not scale Kubernetes.
    /// It does not execute DAG steps.
    ///
    /// It only dispatches an already selected shared run to a runtime queue.
    /// </remarks>
    public interface IAiSharedRunDispatcher
    {
        /// <summary>
        /// Dispatches a shared run to a runtime instance.
        /// </summary>
        /// <param name="request">The dispatch request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The dispatch result.</returns>
        Task<AiSharedRunDispatchResult> DispatchAsync(
            AiSharedRunDispatchRequest request,
            CancellationToken cancellationToken = default);
    }
}