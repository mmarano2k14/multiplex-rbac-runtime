namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Claiming
{
    /// <summary>
    /// Represents a request to atomically claim a pending shared queue item.
    /// </summary>
    public sealed class AiSharedQueueClaimRequest
    {
        /// <summary>
        /// Runtime instance id attempting to claim a pending shared queue item.
        /// </summary>
        public required string RuntimeInstanceId { get; init; }

        /// <summary>
        /// Optional worker id or controller id attempting the claim.
        /// </summary>
        public string? WorkerId { get; init; }

        /// <summary>
        /// Optional tenant id used to restrict claim selection.
        /// </summary>
        public string? TenantId { get; init; }

        /// <summary>
        /// Optional pipeline key used to restrict claim selection.
        /// </summary>
        public string? PipelineKey { get; init; }

        /// <summary>
        /// Claim lease duration.
        /// </summary>
        public TimeSpan ClaimTtl { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Optional id used to correlate logs, metrics, traces, ledger entries,
        /// and dashboard actions across the control plane.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Optional reason explaining why claim was requested.
        /// </summary>
        public string? Reason { get; init; }
    }
}