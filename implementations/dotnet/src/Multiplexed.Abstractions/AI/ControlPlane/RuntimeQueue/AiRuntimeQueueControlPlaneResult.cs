using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue
{
    /// <summary>
    /// Represents an adapter-neutral local runtime queue control-plane result.
    ///
    /// This result targets the queue owned by one runtime instance.
    /// It does not represent the future shared/global queue.
    /// </summary>
    public sealed class AiRuntimeQueueControlPlaneResult
    {
        /// <summary>
        /// Local runtime queue control-plane operation that was executed.
        /// </summary>
        public required AiRuntimeQueueControlPlaneOperation Operation { get; init; }

        /// <summary>
        /// Indicates whether the control-plane operation completed successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Human-readable summary of the operation result.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Runtime run identifier when available.
        /// </summary>
        public string? RunId { get; init; }

        /// <summary>
        /// Durable shared DAG execution identifier when available.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Handle returned when a new run is enqueued.
        /// </summary>
        public AiRuntimeWorkerRunHandle? RunHandle { get; init; }

        /// <summary>
        /// Visibility snapshot for a runtime pipeline run when available.
        /// </summary>
        public AiRuntimePipelineRunState? RunState { get; init; }

        /// <summary>
        /// Visibility snapshot for the local runtime pipeline queue when available.
        /// </summary>
        public AiRuntimePipelineQueueState? QueueState { get; init; }

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