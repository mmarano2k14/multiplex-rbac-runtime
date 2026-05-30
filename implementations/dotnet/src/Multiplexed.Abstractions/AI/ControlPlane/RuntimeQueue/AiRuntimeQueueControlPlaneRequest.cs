using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue
{
    /// <summary>
    /// Represents an adapter-neutral local runtime queue control-plane request.
    ///
    /// This request targets the queue owned by one runtime instance.
    /// It does not represent the future shared/global queue.
    /// </summary>
    public sealed class AiRuntimeQueueControlPlaneRequest
    {
        /// <summary>
        /// Requested local runtime queue control-plane operation.
        /// </summary>
        public required AiRuntimeQueueControlPlaneOperation Operation { get; init; }

        /// <summary>
        /// Runtime run identifier when the operation targets an existing run.
        /// Required for CancelRun, CancelQueuedRun, and GetRunStatus.
        /// </summary>
        public string? RunId { get; init; }

        /// <summary>
        /// Pipeline run request used when enqueueing a new local runtime run.
        /// Required for EnqueueRun.
        /// </summary>
        public AiRuntimePipelineRunRequest? RunRequest { get; init; }

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
        /// Optional reason explaining why the runtime queue control operation was requested.
        /// Useful for audit, diagnostics, and operator history.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Optional runtime instance id when the request is issued from
        /// or targets a specific runtime instance / Kubernetes pod.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Indicates whether the returned run state should be included when available.
        /// </summary>
        public bool IncludeRunState { get; init; } = true;

        /// <summary>
        /// Indicates whether the returned queue state should be included when available.
        /// </summary>
        public bool IncludeQueueState { get; init; } = true;

        /// <summary>
        /// Indicates whether diagnostics should be included when available.
        /// </summary>
        public bool IncludeDiagnostics { get; init; } = true;
    }
}