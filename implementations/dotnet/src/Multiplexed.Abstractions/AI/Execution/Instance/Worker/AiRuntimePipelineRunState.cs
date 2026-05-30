namespace Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Represents an immutable visibility snapshot of a runtime pipeline run.
    ///
    /// This model is intended for control-plane, dashboard, MCP, HTTP API,
    /// CLI, diagnostics, and future Kubernetes visibility.
    /// </summary>
    public sealed class AiRuntimePipelineRunState
    {
        /// <summary>
        /// Runtime run identifier.
        /// </summary>
        public required string RunId { get; init; }

        /// <summary>
        /// Durable shared DAG execution identifier when already known.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Pipeline key associated with the run when available.
        /// </summary>
        public string? PipelineKey { get; init; }

        /// <summary>
        /// Pipeline name associated with the run when available.
        /// </summary>
        public string? PipelineName { get; init; }

        /// <summary>
        /// Runtime instance id that owns the local queue/run.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Current run status.
        /// Examples: queued, running, completed, cancelled, failed, unknown.
        /// </summary>
        public required string Status { get; init; }

        /// <summary>
        /// Indicates whether the run is currently queued locally.
        /// </summary>
        public bool IsQueued { get; init; }

        /// <summary>
        /// Indicates whether the run is currently running locally.
        /// </summary>
        public bool IsRunning { get; init; }

        /// <summary>
        /// Indicates whether cancellation has been requested for this run.
        /// </summary>
        public bool CancellationRequested { get; init; }

        /// <summary>
        /// Optional reason associated with cancellation or failure.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// UTC timestamp when the run was enqueued, if known.
        /// </summary>
        public DateTimeOffset? QueuedAtUtc { get; init; }

        /// <summary>
        /// UTC timestamp when the run started, if known.
        /// </summary>
        public DateTimeOffset? StartedAtUtc { get; init; }

        /// <summary>
        /// UTC timestamp when the run completed, failed, or was cancelled, if known.
        /// </summary>
        public DateTimeOffset? CompletedAtUtc { get; init; }

        /// <summary>
        /// Optional failure message when the run failed.
        /// </summary>
        public string? FailureReason { get; init; }
    }
}