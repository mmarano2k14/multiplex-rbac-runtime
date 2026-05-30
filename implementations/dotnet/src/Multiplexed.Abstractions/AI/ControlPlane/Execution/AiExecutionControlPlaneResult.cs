using Multiplexed.Abstractions.AI.Execution.Control;

namespace Multiplexed.Abstractions.AI.ControlPlane.Execution
{
    /// <summary>
    /// Represents an adapter-neutral execution control-plane result.
    ///
    /// This result can be returned later by HTTP API, MCP, CLI, dashboard,
    /// or Kubernetes control-plane adapters without coupling them directly
    /// to runtime engine internals.
    /// </summary>
    public sealed class AiExecutionControlPlaneResult
    {
        /// <summary>
        /// Durable shared DAG execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Execution control-plane operation that was executed.
        /// </summary>
        public required AiExecutionControlPlaneOperation Operation { get; init; }

        /// <summary>
        /// Indicates whether the control-plane operation completed successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Human-readable summary of the operation result.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Durable execution control state when available.
        /// </summary>
        public AiExecutionControlState? State { get; init; }

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
        /// Optional runtime instance id that handled the control operation.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

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