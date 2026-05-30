namespace Multiplexed.Abstractions.AI.ControlPlane.Execution
{
    /// <summary>
    /// Represents an adapter-neutral execution control-plane request.
    ///
    /// This request can be used later by HTTP API, MCP, CLI, dashboard,
    /// or Kubernetes control-plane adapters without coupling them to
    /// ASP.NET, MCP, Kubernetes, or runtime engine internals.
    /// </summary>
    public sealed class AiExecutionControlPlaneRequest
    {
        /// <summary>
        /// Durable shared DAG execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Requested execution control-plane operation.
        /// </summary>
        public required AiExecutionControlPlaneOperation Operation { get; init; }

        /// <summary>
        /// Optional id used to correlate logs, metrics, traces, ledger entries,
        /// and dashboard actions across the control plane.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Optional identity of the caller requesting the operation.
        /// This can be a user, service account, MCP caller, CLI user,
        /// dashboard operator, or Kubernetes control-plane component.
        /// </summary>
        public string? RequestedBy { get; init; }

        /// <summary>
        /// Optional source adapter that initiated the request.
        /// Examples: http-api, mcp, cli, dashboard, kubernetes-control.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// Optional reason explaining why the execution control operation was requested.
        /// Useful for audit, diagnostics, and operator history.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Optional runtime instance id when the request is issued from
        /// a runtime instance or Kubernetes pod.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Optional waiting key used when submitting human or external input.
        /// </summary>
        public string? WaitingKey { get; init; }

        /// <summary>
        /// Optional human-readable waiting step name used when submitting input.
        /// </summary>
        public string? WaitingStepName { get; init; }

        /// <summary>
        /// Optional input payload used by SubmitHumanInput.
        /// </summary>
        public object? Input { get; init; }

        /// <summary>
        /// Indicates whether the returned durable control state should be included when available.
        /// </summary>
        public bool IncludeState { get; init; } = true;

        /// <summary>
        /// Indicates whether diagnostics should be included when available.
        /// </summary>
        public bool IncludeDiagnostics { get; init; } = true;
    }
}