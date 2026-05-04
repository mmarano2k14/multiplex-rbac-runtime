using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Represents the final decision produced by the retry engine.
    /// </summary>
    public sealed class AiRetryDecision
    {
        private AiRetryDecision(
            AiRetryDecisionKind kind,
            TimeSpan? delay = null,
            string? reason = null)
        {
            Kind = kind;
            Delay = delay;
            Reason = reason;
        }

        /// <summary>
        /// Gets the decision kind.
        /// </summary>
        public AiRetryDecisionKind Kind { get; }

        /// <summary>
        /// Gets the delay before the next retry, if applicable.
        /// </summary>
        public TimeSpan? Delay { get; }

        /// <summary>
        /// Gets the optional reason associated with the decision.
        /// </summary>
        public string? Reason { get; }

        public static AiRetryDecision Retry(TimeSpan delay, string? reason = null)
            => new AiRetryDecision(AiRetryDecisionKind.Retry, delay, reason);

        public static AiRetryDecision Fail(string? reason = null)
            => new AiRetryDecision(AiRetryDecisionKind.Fail, null, reason);

        public static AiRetryDecision Stop(string? reason = null)
            => new AiRetryDecision(AiRetryDecisionKind.Stop, null, reason);
    }
}