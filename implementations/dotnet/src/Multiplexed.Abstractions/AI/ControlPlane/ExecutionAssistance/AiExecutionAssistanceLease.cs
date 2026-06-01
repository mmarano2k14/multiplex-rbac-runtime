namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents a time-bound permission for a runtime instance to assist
    /// an execution owned by another primary runtime instance.
    /// </summary>
    public sealed class AiExecutionAssistanceLease
    {
        /// <summary>
        /// Gets the unique assistance lease identifier.
        /// </summary>
        public required string LeaseId { get; init; }

        /// <summary>
        /// Gets the execution identifier being assisted.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the primary runtime instance that owns the execution lifecycle.
        /// </summary>
        public required string PrimaryRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the helper runtime instance allowed to assist the execution.
        /// </summary>
        public required string HelperRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the maximum number of helper workers allowed for this lease.
        /// </summary>
        public int MaxWorkers { get; init; }

        /// <summary>
        /// Gets the current lease status.
        /// </summary>
        public AiExecutionAssistanceStatus Status { get; init; } =
            AiExecutionAssistanceStatus.Granted;

        /// <summary>
        /// Gets the UTC timestamp when the lease was granted.
        /// </summary>
        public DateTimeOffset GrantedAtUtc { get; init; } =
            DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the UTC timestamp when the lease expires.
        /// </summary>
        public DateTimeOffset ExpiresAtUtc { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the lease was started by the helper instance.
        /// </summary>
        public DateTimeOffset? StartedAtUtc { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the lease was completed, released, expired, revoked, or failed.
        /// </summary>
        public DateTimeOffset? CompletedAtUtc { get; init; }

        /// <summary>
        /// Gets the reason why the lease was granted, denied, released, revoked, or failed.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the assistance lease.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}