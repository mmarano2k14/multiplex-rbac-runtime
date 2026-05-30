namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances
{
    /// <summary>
    /// Represents an adapter-neutral runtime instance control-plane result.
    ///
    /// This result can be returned later by HTTP API, MCP, CLI, dashboard,
    /// Kubernetes control-plane adapters, or shared admission components.
    /// </summary>
    public sealed class AiRuntimeInstanceControlPlaneResult
    {
        /// <summary>
        /// Runtime instance control-plane operation that was executed.
        /// </summary>
        public required AiRuntimeInstanceControlPlaneOperation Operation { get; init; }

        /// <summary>
        /// Indicates whether the control-plane operation completed successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Human-readable summary of the operation result.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Runtime instance identifier when available.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Runtime instance snapshot when the operation targets one instance.
        /// </summary>
        public AiRuntimeInstanceSnapshot? Instance { get; init; }

        /// <summary>
        /// Runtime instance snapshots when listing instances.
        /// </summary>
        public IReadOnlyList<AiRuntimeInstanceSnapshot> Instances { get; init; } =
            Array.Empty<AiRuntimeInstanceSnapshot>();

        /// <summary>
        /// Optional diagnostics produced by the control-plane operation.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Optional id used to correlate logs, metrics, traces, ledger entries,
        /// and dashboard actions across the control plane.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Optional caller identity.
        /// </summary>
        public string? RequestedBy { get; init; }

        /// <summary>
        /// UTC timestamp when the control operation started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; init; }

        /// <summary>
        /// UTC timestamp when the control operation completed.
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; init; }

        /// <summary>
        /// Duration of the control operation in milliseconds.
        /// Useful for metrics and Grafana dashboards.
        /// </summary>
        public long DurationMs { get; init; }

        /// <summary>
        /// Optional failure reason if the control operation failed.
        /// </summary>
        public string? FailureReason { get; init; }
    }
}