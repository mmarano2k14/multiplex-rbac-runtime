namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Represents the outcome of evaluating retry behavior for a failed step.
    /// </summary>
    public enum AiRetryDecisionKind
    {
        /// <summary>
        /// No retry policy was available or applicable.
        /// </summary>
        NoPolicy = 0,

        /// <summary>
        /// A retry should be scheduled for the failed step.
        /// </summary>
        RetryScheduled = 1,

        /// <summary>
        /// The retry budget has been exhausted and the step should fail permanently.
        /// </summary>
        RetryExhausted = 2,

        /// <summary>
        /// The failure is not retryable and the step should fail permanently.
        /// </summary>
        NonRetryableFailure = 3
    }
}