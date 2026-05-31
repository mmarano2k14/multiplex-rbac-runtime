namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController.Scaling
{
    /// <summary>
    /// Publishes runtime scale-out requests produced by the shared controller.
    /// </summary>
    /// <remarks>
    /// The publisher is intentionally infrastructure-neutral.
    ///
    /// Implementations may:
    /// - do nothing in local mode
    /// - publish to Redis
    /// - publish to a message bus
    /// - call a Kubernetes adapter
    /// - notify an external control plane
    ///
    /// This interface does not execute DAG steps and does not directly own
    /// admission decisions.
    /// </remarks>
    public interface IAiRuntimeScaleOutRequestPublisher
    {
        /// <summary>
        /// Publishes a runtime scale-out request.
        /// </summary>
        /// <param name="request">The scale-out request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The publication result.</returns>
        Task<AiRuntimeScaleOutRequestResult> PublishAsync(
            AiRuntimeScaleOutRequest request,
            CancellationToken cancellationToken = default);
    }
}