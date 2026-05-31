namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.SharedInstance
{
    /// <summary>
    /// Registry of shared runtime instances addressable by the control-plane.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Allows a dispatcher to resolve a RuntimeInstanceId to an object
    ///   capable of receiving a run.
    ///
    /// DIFFERENCE WITH IAiRuntimeInstanceRegistry:
    /// - IAiRuntimeInstanceRegistry describes visible runtime instance state,
    ///   heartbeat, capacity, and snapshots.
    /// - IAiSharedRuntimeInstanceRegistry resolves dispatchable runtime instance
    ///   endpoints/adapters.
    ///
    /// This registry is required for Kubernetes-ready remote dispatch.
    /// </remarks>
    public interface IAiSharedRuntimeInstanceRegistry
    {
        /// <summary>
        /// Registers a dispatchable shared runtime instance.
        /// </summary>
        Task RegisterAsync(
            IAiSharedRuntimeInstance instance,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a dispatchable shared runtime instance by identifier.
        /// </summary>
        Task<IAiSharedRuntimeInstance?> GetAsync(
            string runtimeInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all registered shared runtime instances.
        /// </summary>
        Task<IReadOnlyCollection<IAiSharedRuntimeInstance>> ListAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unregisters a shared runtime instance.
        /// </summary>
        Task<bool> UnregisterAsync(
            string runtimeInstanceId,
            CancellationToken cancellationToken = default);
    }
}