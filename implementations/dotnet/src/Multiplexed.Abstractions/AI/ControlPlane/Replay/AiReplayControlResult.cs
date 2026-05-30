using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Observability.Tracing;

namespace Multiplexed.Abstractions.AI.ControlPlane.Replay
{
    /// <summary>
    /// Represents an adapter-neutral replay control-plane result.
    ///
    /// This result can be returned later by HTTP API, MCP, CLI, dashboard,
    /// or Kubernetes control-plane adapters without coupling them directly
    /// to replay engine internals.
    /// </summary>
    public sealed class AiReplayControlResult
    {
        /// <summary>
        /// Durable shared DAG execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Replay control-plane operation that was executed.
        /// </summary>
        public required AiReplayOperation Operation { get; init; }

        /// <summary>
        /// Indicates whether the control-plane operation completed successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Human-readable summary of the operation result.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Optional replay report returned by the replay engine.
        /// </summary>
        public AiExecutionReplayReport? Report { get; init; }

        /// <summary>
        /// Decision ledger entries associated with the execution.
        ///
        /// Intended to be exportable later to Kibana, OpenSearch,
        /// Elasticsearch, or any structured observability sink.
        /// </summary>
        public IReadOnlyList<AiDecisionLedgerEntry> Ledger { get; init; } =
            Array.Empty<AiDecisionLedgerEntry>();

        /// <summary>
        /// Trace timeline events associated with the execution.
        ///
        /// Intended to be exportable later to tracing backends, dashboards,
        /// or Grafana-compatible observability pipelines.
        /// </summary>
        public IReadOnlyList<AiTraceEvent> Timeline { get; init; } =
            Array.Empty<AiTraceEvent>();

        /// <summary>
        /// Optional diagnostics produced by replay validation, audit,
        /// restore, report generation, or control-plane checks.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Optional deterministic validation result when available.
        /// </summary>
        public bool? Deterministic { get; init; }

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