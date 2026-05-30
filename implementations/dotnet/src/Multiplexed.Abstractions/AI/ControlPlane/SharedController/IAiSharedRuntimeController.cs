namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController
{
    /// <summary>
    /// Defines the high-level shared runtime controller facade.
    /// </summary>
    /// <remarks>
    /// The shared runtime controller sits above:
    /// - run admission
    /// - runtime instance registry
    /// - local runtime queue control
    /// - future shared/global queue
    /// - future Kubernetes scale-out adapter
    ///
    /// Important:
    /// This abstraction does not execute DAG steps.
    /// It does not claim work.
    /// It does not directly create Kubernetes pods.
    /// It does not replace local runtime queues.
    /// </remarks>
    public interface IAiSharedRuntimeController
    {
        /// <summary>
        /// Executes a shared runtime controller operation.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The shared runtime controller result.</returns>
        Task<AiSharedRuntimeControllerResult> ExecuteAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Submits a new run to the shared runtime controller.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The shared runtime controller result.</returns>
        Task<AiSharedRuntimeControllerResult> SubmitRunAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a shared run known by the controller.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The shared runtime controller result.</returns>
        Task<AiSharedRuntimeControllerResult> GetRunAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists shared runs known by the controller.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The shared runtime controller result.</returns>
        Task<AiSharedRuntimeControllerResult> ListRunsAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a shared run before or after assignment.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The shared runtime controller result.</returns>
        Task<AiSharedRuntimeControllerResult> CancelRunAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default);
    }
}