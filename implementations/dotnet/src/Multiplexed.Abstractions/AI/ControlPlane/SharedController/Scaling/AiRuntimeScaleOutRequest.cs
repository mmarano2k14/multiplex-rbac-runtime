using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Store;

namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController.Scaling
{
    /// <summary>
    /// Represents a request to scale out runtime capacity.
    /// </summary>
    /// <remarks>
    /// This request is emitted when admission determines that no current runtime
    /// instance can accept a run, but the system is allowed to request additional
    /// capacity.
    ///
    /// This contract does not create Kubernetes pods directly.
    /// It allows adapters to publish or handle scale-out requests.
    /// </remarks>
    public sealed class AiRuntimeScaleOutRequest
    {
        /// <summary>
        /// Shared run that triggered the scale-out request.
        /// </summary>
        public required AiSharedRunRecord SharedRun { get; init; }

        /// <summary>
        /// Requested shared run id.
        /// </summary>
        public required string SharedRunId { get; init; }

        /// <summary>
        /// Optional tenant id.
        /// </summary>
        public string? TenantId { get; init; }

        /// <summary>
        /// Optional pipeline key.
        /// </summary>
        public string? PipelineKey { get; init; }

        /// <summary>
        /// Current visible runtime instance count.
        /// </summary>
        public int VisibleInstanceCount { get; init; }

        /// <summary>
        /// Current available runtime instance count.
        /// </summary>
        public int AvailableInstanceCount { get; init; }

        /// <summary>
        /// Current total runtime instance count.
        /// </summary>
        public int CurrentInstanceCount { get; init; }

        /// <summary>
        /// Maximum allowed runtime instance count.
        /// </summary>
        public int? MaxInstanceCount { get; init; }

        /// <summary>
        /// Optional correlation id.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Optional requester identity.
        /// </summary>
        public string? RequestedBy { get; init; }

        /// <summary>
        /// Optional source label.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// Optional reason.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Optional metadata.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}