using System;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Classifies failures to determine whether they are eligible for retry.
    /// </summary>
    /// <remarks>
    /// Classifiers provide a lightweight decision point before retry policies are evaluated.
    /// They are useful for separating transient failures from validation, authorization,
    /// configuration, or other permanent failures.
    /// </remarks>
    public interface IAiRetryClassifier
    {
        /// <summary>
        /// Determines whether the failure described by the retry context is retryable.
        /// </summary>
        /// <param name="context">The retry context describing the failed step.</param>
        /// <returns>
        /// <c>true</c> when the failure may be retried; otherwise, <c>false</c>.
        /// </returns>
        bool IsRetryable(AiRetryContext context);
    }
}