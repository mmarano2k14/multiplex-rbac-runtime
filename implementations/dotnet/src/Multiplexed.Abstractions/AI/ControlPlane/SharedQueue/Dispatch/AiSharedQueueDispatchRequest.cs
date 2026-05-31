namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Dispatch
{
    /// <summary>
    /// Represents a request to claim and dispatch one pending shared queue item.
    /// </summary>
    public sealed class AiSharedQueueDispatchRequest
    {
        /// <summary>
        /// Runtime instance id that wants to claim and dispatch a queued shared run.
        /// </summary>
        public required string RuntimeInstanceId { get; init; }

        /// <summary>
        /// Optional worker id or controller id performing the dispatch.
        /// </summary>
        public string? WorkerId { get; init; }

        /// <summary>
        /// Optional tenant filter.
        /// </summary>
        public string? TenantId { get; init; }

        /// <summary>
        /// Optional pipeline filter.
        /// </summary>
        public string? PipelineKey { get; init; }

        /// <summary>
        /// Claim lease duration used while dispatching.
        /// </summary>
        public TimeSpan ClaimTtl { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Optional correlation id used for logs, metrics, tracing, ledger, and dashboard events.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Optional identity requesting the queue dispatch.
        /// </summary>
        public string? RequestedBy { get; init; }

        /// <summary>
        /// Optional source adapter requesting the queue dispatch.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// Optional reason explaining why queue dispatch was requested.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Optional metadata for routing, dashboard, Kubernetes labels, or diagnostics.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}