namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances
{
    /// <summary>
    /// Defines options for the runtime instance control-plane facade.
    ///
    /// These options only control the high-level control-plane layer.
    /// They do not modify runtime instances, local queues, workers, or Kubernetes infrastructure.
    /// </summary>
    public sealed class AiRuntimeInstanceControlPlaneOptions
    {
        /// <summary>
        /// Enables runtime instance registration operations.
        /// </summary>
        public bool EnableRegister { get; init; } = true;

        /// <summary>
        /// Enables runtime instance heartbeat operations.
        /// </summary>
        public bool EnableHeartbeat { get; init; } = true;

        /// <summary>
        /// Enables runtime instance lookup operations.
        /// </summary>
        public bool EnableGetInstance { get; init; } = true;

        /// <summary>
        /// Enables runtime instance listing operations.
        /// </summary>
        public bool EnableListInstances { get; init; } = true;

        /// <summary>
        /// Enables marking runtime instances as draining.
        /// </summary>
        public bool EnableMarkDraining { get; init; } = true;

        /// <summary>
        /// Enables runtime instance unregister operations.
        /// </summary>
        public bool EnableUnregister { get; init; } = true;

        /// <summary>
        /// When enabled, expected operational failures should be returned as
        /// structured failed results instead of being thrown to the caller.
        /// </summary>
        public bool ReturnFailureResultInsteadOfThrowing { get; init; } = true;

        /// <summary>
        /// Enables controller-level duration measurement.
        ///
        /// This is useful for future Grafana metrics and control-plane diagnostics.
        /// </summary>
        public bool MeasureDuration { get; init; } = true;
    }
}