namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController
{
    /// <summary>
    /// Represents an adapter-neutral shared runtime controller result.
    /// </summary>
    /// <remarks>
    /// This result describes the shared controller decision/state.
    /// It does not represent the final durable DAG execution result unless
    /// an execution has already been created and correlated.
    /// </remarks>
    public sealed class AiSharedRuntimeControllerResult
    {
        /// <summary>
        /// Shared runtime controller operation that was executed.
        /// </summary>
        public required AiSharedRuntimeControllerOperation Operation { get; init; }

        /// <summary>
        /// Indicates whether the shared controller operation completed successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Human-readable summary of the operation result.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Shared controller run identifier when available.
        /// </summary>
        public string? SharedRunId { get; init; }

        /// <summary>
        /// Local runtime queue run identifier when the shared run has been dispatched
        /// to a runtime instance.
        /// </summary>
        public string? LocalRunId { get; init; }

        /// <summary>
        /// Durable DAG execution identifier when available.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Runtime instance selected or assigned for this shared run when available.
        /// </summary>
        public string? AssignedRuntimeInstanceId { get; init; }

        /// <summary>
        /// Shared run record when the operation targets a single run.
        /// </summary>
        public AiSharedRunRecord? Run { get; init; }

        /// <summary>
        /// Shared run records when listing runs.
        /// </summary>
        public IReadOnlyList<AiSharedRunRecord> Runs { get; init; } =
            Array.Empty<AiSharedRunRecord>();

        /// <summary>
        /// Optional diagnostics produced by the shared controller operation.
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