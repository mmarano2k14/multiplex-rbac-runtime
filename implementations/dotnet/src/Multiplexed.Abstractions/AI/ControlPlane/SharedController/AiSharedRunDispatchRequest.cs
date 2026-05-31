using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue;

namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController
{
    /// <summary>
    /// Represents a request to dispatch a shared run to a runtime instance.
    /// </summary>
    /// <remarks>
    /// Dispatching is the bridge between the shared controller/queue layer
    /// and a target runtime instance local queue.
    ///
    /// This request does not execute DAG steps directly.
    /// It only asks a dispatcher to submit the shared run to the selected runtime queue.
    /// </remarks>
    public sealed class AiSharedRunDispatchRequest
    {
        /// <summary>
        /// Shared run record to dispatch.
        /// </summary>
        public required AiSharedRunRecord SharedRun { get; init; }

        /// <summary>
        /// Optional shared queue item associated with this dispatch.
        /// Required when dispatching from a claimed shared queue item.
        /// </summary>
        public AiSharedQueueItem? QueueItem { get; init; }

        /// <summary>
        /// Runtime instance id selected for dispatch.
        /// </summary>
        public required string RuntimeInstanceId { get; init; }

        /// <summary>
        /// Optional claim token that owns the shared queue item.
        /// </summary>
        public string? ClaimToken { get; init; }

        /// <summary>
        /// Optional id used to correlate logs, metrics, traces, ledger entries,
        /// and dashboard actions across the control plane.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Optional identity requesting the dispatch.
        /// </summary>
        public string? RequestedBy { get; init; }

        /// <summary>
        /// Optional source adapter requesting the dispatch.
        /// Examples: shared-controller, mcp, cli, api, kubernetes-control.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// Optional reason explaining why dispatch was requested.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Optional metadata for future routing, diagnostics, dashboard, or Kubernetes labels.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}