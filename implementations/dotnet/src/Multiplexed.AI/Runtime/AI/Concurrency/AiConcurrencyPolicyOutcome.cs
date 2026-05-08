namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Represents the result returned by a concurrency policy.
    /// </summary>
    public sealed class AiConcurrencyPolicyOutcome
    {
        /// <summary>
        /// Gets or sets a value indicating whether execution is allowed.
        /// </summary>
        public bool IsAllowed { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional reason explaining the policy decision.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets an optional suggested retry delay.
        /// </summary>
        public TimeSpan? RetryAfter { get; set; }
    }
}