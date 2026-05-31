namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.Control
{
    /// <summary>
    /// Defines high-level runtime instance control-plane operations.
    /// </summary>
    public enum AiRuntimeInstanceControlPlaneOperation
    {
        /// <summary>
        /// Registers or updates a runtime instance.
        /// </summary>
        Register = 0,

        /// <summary>
        /// Records a heartbeat for a runtime instance.
        /// </summary>
        Heartbeat = 1,

        /// <summary>
        /// Gets a registered runtime instance snapshot.
        /// </summary>
        GetInstance = 2,

        /// <summary>
        /// Lists registered runtime instance snapshots.
        /// </summary>
        ListInstances = 3,

        /// <summary>
        /// Marks a runtime instance as draining.
        /// </summary>
        MarkDraining = 4,

        /// <summary>
        /// Unregisters a runtime instance by marking it as stopped.
        /// </summary>
        Unregister = 5
    }
}