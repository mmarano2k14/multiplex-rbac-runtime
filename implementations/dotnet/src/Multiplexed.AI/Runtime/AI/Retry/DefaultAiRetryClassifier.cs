using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Provides the default retry classification behavior.
    /// </summary>
    /// <remarks>
    /// The default classifier treats failures with an exception or failure reason as retryable.
    /// More advanced classifiers can distinguish transient provider failures from permanent
    /// validation, authorization, configuration, or domain failures.
    /// </remarks>
    public sealed class DefaultAiRetryClassifier : IAiRetryClassifier
    {
        /// <inheritdoc />
        public bool IsRetryable(AiRetryContext context)
        {
            return context.Exception is not null || !string.IsNullOrWhiteSpace(context.FailureReason);
        }
    }
}