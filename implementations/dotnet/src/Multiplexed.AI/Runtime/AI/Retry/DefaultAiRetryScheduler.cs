using System;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Provides the default retry delay calculation for AI retry policies.
    /// </summary>
    /// <remarks>
    /// The scheduler computes delays only. It does not decide whether a failure is retryable
    /// and does not mutate execution state.
    /// </remarks>
    public sealed class DefaultAiRetryScheduler : IAiRetryScheduler
    {
        private static readonly Random JitterRandom = new();

        /// <inheritdoc />
        public TimeSpan ComputeDelay(AiRetryPolicyDefinition definition, int retryCount)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var attempt = Math.Max(1, retryCount + 1);
            var baseDelayMs = Math.Max(0, definition.BaseDelayMs);

            var delayMs = definition.Strategy switch
            {
                AiRetryBackoffStrategy.Fixed => baseDelayMs,
                AiRetryBackoffStrategy.Linear => baseDelayMs * attempt,
                AiRetryBackoffStrategy.Exponential => baseDelayMs * Math.Pow(2, attempt - 1),
                _ => baseDelayMs
            };

            if (definition.MaxDelayMs.HasValue)
            {
                delayMs = Math.Min(delayMs, definition.MaxDelayMs.Value);
            }

            if (definition.Jitter && delayMs > 0)
            {
                delayMs = ApplyBoundedJitter(delayMs);
            }

            return TimeSpan.FromMilliseconds(delayMs);
        }

        private static double ApplyBoundedJitter(double delayMs)
        {
            lock (JitterRandom)
            {
                var factor = 0.8 + (JitterRandom.NextDouble() * 0.4);
                return delayMs * factor;
            }
        }
    }
}