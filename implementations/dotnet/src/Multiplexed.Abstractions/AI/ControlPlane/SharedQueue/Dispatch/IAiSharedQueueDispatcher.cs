namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Dispatch
{
    /// <summary>
    /// Defines a dispatcher that claims pending shared queue items and dispatches them to runtime instances.
    /// </summary>
    /// <remarks>
    /// This is the bridge between the global shared queue and the shared run dispatcher.
    ///
    /// Responsibilities:
    /// - claim one pending queue item atomically
    /// - load the shared run record
    /// - dispatch it to the selected runtime instance
    /// - mark queue and shared run state after success
    /// - requeue the item when dispatch fails
    ///
    /// It does not decide admission.
    /// It does not scale Kubernetes.
    /// It does not execute DAG steps.
    /// </remarks>
    public interface IAiSharedQueueDispatcher
    {
        /// <summary>
        /// Claims and dispatches one pending shared queue item.
        /// </summary>
        /// <param name="request">The queue dispatch request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The queue dispatch result.</returns>
        Task<AiSharedQueueDispatchResult> DispatchNextAsync(
            AiSharedQueueDispatchRequest request,
            CancellationToken cancellationToken = default);
    }
}