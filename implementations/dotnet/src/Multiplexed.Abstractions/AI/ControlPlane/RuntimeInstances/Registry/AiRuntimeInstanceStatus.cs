namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.Registry
{
    /// <summary>
    /// Defines the visibility status of a runtime instance.
    /// </summary>
    public enum AiRuntimeInstanceStatus
    {
        /// <summary>
        /// The runtime instance is registered but its current health is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The runtime instance is alive and able to accept work.
        /// </summary>
        Ready = 1,

        /// <summary>
        /// The runtime instance is alive but currently under pressure.
        /// </summary>
        Busy = 2,

        /// <summary>
        /// The runtime instance is alive but its local queue is paused.
        /// </summary>
        Paused = 3,

        /// <summary>
        /// The runtime instance is draining and should not receive new runs.
        /// </summary>
        Draining = 4,

        /// <summary>
        /// The runtime instance has not sent a heartbeat within the expected interval.
        /// </summary>
        Unhealthy = 5,

        /// <summary>
        /// The runtime instance has been explicitly unregistered.
        /// </summary>
        Stopped = 6
    }
}