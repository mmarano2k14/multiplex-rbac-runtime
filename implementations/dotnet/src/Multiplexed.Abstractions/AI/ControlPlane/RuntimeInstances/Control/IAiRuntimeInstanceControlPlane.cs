namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.Control
{
    /// <summary>
    /// Defines the high-level runtime instance control-plane facade.
    ///
    /// This abstraction exposes runtime instance registration, heartbeat,
    /// lookup, listing, draining, and unregister operations without coupling
    /// external adapters to registry implementation details.
    ///
    /// Intended future callers:
    /// - HTTP API
    /// - MCP server
    /// - CLI
    /// - Dashboard
    /// - Kubernetes control-plane pod
    /// - Shared run admission controller
    ///
    /// Important:
    /// This abstraction does not create Kubernetes pods, scale deployments,
    /// execute DAG steps, claim work, or replace local runtime queues.
    /// </summary>
    public interface IAiRuntimeInstanceControlPlane
    {
        /// <summary>
        /// Executes a runtime instance control-plane operation.
        /// </summary>
        /// <param name="request">The runtime instance control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The runtime instance control-plane result.</returns>
        Task<AiRuntimeInstanceControlPlaneResult> ExecuteAsync(
            AiRuntimeInstanceControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers or updates a runtime instance.
        /// </summary>
        /// <param name="request">The runtime instance control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The runtime instance control-plane result.</returns>
        Task<AiRuntimeInstanceControlPlaneResult> RegisterAsync(
            AiRuntimeInstanceControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a runtime instance heartbeat.
        /// </summary>
        /// <param name="request">The runtime instance control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The runtime instance control-plane result.</returns>
        Task<AiRuntimeInstanceControlPlaneResult> HeartbeatAsync(
            AiRuntimeInstanceControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a registered runtime instance snapshot.
        /// </summary>
        /// <param name="request">The runtime instance control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The runtime instance control-plane result.</returns>
        Task<AiRuntimeInstanceControlPlaneResult> GetInstanceAsync(
            AiRuntimeInstanceControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists registered runtime instance snapshots.
        /// </summary>
        /// <param name="request">The runtime instance control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The runtime instance control-plane result.</returns>
        Task<AiRuntimeInstanceControlPlaneResult> ListInstancesAsync(
            AiRuntimeInstanceControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a runtime instance as draining.
        /// </summary>
        /// <param name="request">The runtime instance control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The runtime instance control-plane result.</returns>
        Task<AiRuntimeInstanceControlPlaneResult> MarkDrainingAsync(
            AiRuntimeInstanceControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unregisters a runtime instance by marking it as stopped.
        /// </summary>
        /// <param name="request">The runtime instance control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The runtime instance control-plane result.</returns>
        Task<AiRuntimeInstanceControlPlaneResult> UnregisterAsync(
            AiRuntimeInstanceControlPlaneRequest request,
            CancellationToken cancellationToken = default);
    }
}