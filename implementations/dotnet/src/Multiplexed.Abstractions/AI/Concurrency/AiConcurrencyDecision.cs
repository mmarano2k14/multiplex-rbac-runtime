namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Represents the result of a concurrency acquisition attempt.
    /// </summary>
    public sealed class AiConcurrencyDecision
    {
        /// <summary>
        /// Gets the concurrency decision kind.
        /// </summary>
        public AiConcurrencyDecisionKind Kind { get; init; }

        /// <summary>
        /// Gets a value indicating whether execution capacity was acquired.
        /// </summary>
        public bool Allowed => Kind == AiConcurrencyDecisionKind.Allowed;

        /// <summary>
        /// Gets an optional reason explaining why the concurrency request was denied.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets an optional delay after which the runtime may retry acquisition.
        /// </summary>
        public TimeSpan? RetryAfter { get; init; }

        /// <summary>
        /// Creates an allowed concurrency decision.
        /// </summary>
        /// <returns>An allowed concurrency decision.</returns>
        public static AiConcurrencyDecision Allow()
        {
            return new AiConcurrencyDecision
            {
                Kind = AiConcurrencyDecisionKind.Allowed
            };
        }

        /// <summary>
        /// Creates a denied concurrency decision.
        /// </summary>
        /// <param name="reason">The reason why acquisition was denied.</param>
        /// <param name="retryAfter">The optional delay after which acquisition may be retried.</param>
        /// <returns>A denied concurrency decision.</returns>
        public static AiConcurrencyDecision Deny(
            string? reason = null,
            TimeSpan? retryAfter = null)
        {
            return new AiConcurrencyDecision
            {
                Kind = AiConcurrencyDecisionKind.Denied,
                Reason = reason,
                RetryAfter = retryAfter
            };
        }
    }
}