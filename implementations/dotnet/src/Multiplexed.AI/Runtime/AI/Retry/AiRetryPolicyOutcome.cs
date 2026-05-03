using System;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Represents the retry-specific outcome produced by an AI retry policy.
    /// </summary>
    /// <remarks>
    /// This outcome is specific to the retry domain. It is transported through
    /// <c>AiPolicyResultGeneric&lt;AiRetryPolicyOutcome&gt;</c> and interpreted by
    /// the retry engine to produce the final retry decision.
    /// </remarks>
    public sealed class AiRetryPolicyOutcome
    {
        /// <summary>
        /// Gets or sets a value indicating whether the evaluated failure is retryable.
        /// </summary>
        public bool IsRetryable { get; init; }

        /// <summary>
        /// Gets or sets the optional reason explaining why the failure is retryable or not retryable.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets or sets an optional delay suggested by the policy before the next retry attempt.
        /// </summary>
        /// <remarks>
        /// The retry engine remains responsible for the final delay decision. This value
        /// is only a policy suggestion and may be overridden by retry budget, backoff,
        /// jitter, or runtime safety rules.
        /// </remarks>
        public TimeSpan? SuggestedDelay { get; init; }
    }
}