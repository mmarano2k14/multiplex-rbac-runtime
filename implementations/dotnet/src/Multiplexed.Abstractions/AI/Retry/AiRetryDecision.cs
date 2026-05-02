using System;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Represents the final retry decision produced for a failed AI step.
    /// </summary>
    public sealed class AiRetryDecision
    {
        private AiRetryDecision(
            AiRetryDecisionKind kind,
            string? policyKey,
            string? reason,
            TimeSpan? delay,
            bool retryable)
        {
            Kind = kind;
            PolicyKey = policyKey;
            Reason = reason;
            Delay = delay;
            Retryable = retryable;
        }

        /// <summary>
        /// Gets the decision kind.
        /// </summary>
        public AiRetryDecisionKind Kind { get; }

        /// <summary>
        /// Gets the policy key that produced the decision, when available.
        /// </summary>
        public string? PolicyKey { get; }

        /// <summary>
        /// Gets the human-readable reason associated with the decision.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Gets the delay before the next retry attempt, when a retry is scheduled.
        /// </summary>
        public TimeSpan? Delay { get; }

        /// <summary>
        /// Gets a value indicating whether the failure is retryable.
        /// </summary>
        public bool Retryable { get; }

        /// <summary>
        /// Creates a decision that schedules a retry attempt.
        /// </summary>
        public static AiRetryDecision RetryScheduled(string policyKey, TimeSpan delay, string? reason = null)
        {
            return new AiRetryDecision(AiRetryDecisionKind.RetryScheduled, policyKey, reason, delay, retryable: true);
        }

        /// <summary>
        /// Creates a decision indicating that retry attempts have been exhausted.
        /// </summary>
        public static AiRetryDecision RetryExhausted(string? policyKey = null, string? reason = null)
        {
            return new AiRetryDecision(AiRetryDecisionKind.RetryExhausted, policyKey, reason, delay: null, retryable: false);
        }

        /// <summary>
        /// Creates a decision indicating that the failure should not be retried.
        /// </summary>
        public static AiRetryDecision NonRetryable(string? policyKey = null, string? reason = null)
        {
            return new AiRetryDecision(AiRetryDecisionKind.NonRetryableFailure, policyKey, reason, delay: null, retryable: false);
        }

        /// <summary>
        /// Creates a decision indicating that no retry policy was available.
        /// </summary>
        public static AiRetryDecision NoPolicy(string? reason = null)
        {
            return new AiRetryDecision(AiRetryDecisionKind.NoPolicy, policyKey: null, reason, delay: null, retryable: false);
        }
    }
}