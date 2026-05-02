using System;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Computes retry delays from retry configuration and attempt counters.
    /// </summary>
    /// <remarks>
    /// The scheduler is responsible only for delay calculation. It does not mutate
    /// execution state and does not decide whether a failure is retryable.
    /// </remarks>
    public interface IAiRetryScheduler
    {
        /// <summary>
        /// Computes the delay before the next retry attempt.
        /// </summary>
        /// <param name="definition">The retry policy definition associated with the step.</param>
        /// <param name="retryCount">The current retry count already consumed by the step.</param>
        /// <returns>The computed retry delay.</returns>
        TimeSpan ComputeDelay(AiRetryPolicyDefinition definition, int retryCount);
    }
}