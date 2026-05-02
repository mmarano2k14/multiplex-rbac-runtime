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
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the reason associated with the latest retry decision.
        /// </summary>
        public string? RetryReason { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the latest retry scheduling event.
        /// </summary>
        public DateTime? LastRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the next retry attempt becomes eligible.
        /// </summary>
        public DateTime? NextRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the policy key that produced the latest retry decision.
        /// </summary>
        public string? LastRetryPolicyKey { get; set; }
    }
}