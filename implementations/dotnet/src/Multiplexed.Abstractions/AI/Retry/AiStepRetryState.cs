namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Represents the runtime retry state of an AI execution step.
    /// </summary>
    /// <remarks>
    /// This state is mutable execution data. It tracks retry attempts, scheduling,
    /// and the last retry decision applied to the step.
    /// </remarks>
    public sealed class AiStepRetryState
    {
        /// <summary>
        /// Gets or sets the number of retry attempts already consumed by the step.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>0</c>, indicating that no retry has been performed yet.
        /// </remarks>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the reason associated with the latest retry decision.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> indicates that no retry decision has been recorded yet.
        /// </remarks>
        public string? RetryReason { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the latest retry scheduling event.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> indicates that no retry has been scheduled yet.
        /// </remarks>
        public DateTime? LastRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the next retry attempt becomes eligible.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> indicates that no retry is currently scheduled.
        /// When set, the retry window opens when the value is less than or equal to the current UTC time.
        /// </remarks>
        public DateTime? NextRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the policy key that produced the latest retry decision.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> indicates that no retry policy has been applied yet.
        /// </remarks>
        public string? LastRetryPolicyKey { get; set; }

        /// <summary>
        /// Gets a value indicating whether a retry is currently scheduled.
        /// </summary>
        /// <remarks>
        /// This is a convenience helper used to determine whether the step is waiting for a retry window.
        /// </remarks>
        public bool HasPendingRetry => NextRetryAtUtc.HasValue;
    }
}