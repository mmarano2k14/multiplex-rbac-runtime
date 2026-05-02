using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Provides the default transient retry policy for failed AI steps.
    /// </summary>
    /// <remarks>
    /// This policy relies on the resolved retry definition to compute the retry delay.
    /// It is intended as a safe default for transient runtime, provider, retrieval,
    /// or infrastructure failures.
    /// </remarks>
    [AiPolicy("retry.transient.default", Kind = AiPolicyKind.Retry)]
    public sealed class DefaultTransientRetryPolicy : IAiRetryPolicy
    {
        private readonly IAiRetryScheduler scheduler;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTransientRetryPolicy"/> class.
        /// </summary>
        /// <param name="scheduler">The retry scheduler used to compute the next retry delay.</param>
        public DefaultTransientRetryPolicy(IAiRetryScheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <inheritdoc />
        public string Key => "retry.transient.default";

        /// <inheritdoc />
        public AiPolicyKind Kind => AiPolicyKind.Retry;

        /// <inheritdoc />
        public ValueTask<AiRetryDecision> EvaluateAsync(
            AiRetryContext context,
            CancellationToken cancellationToken = default)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Retry is null)
            {
                return ValueTask.FromResult(
                    AiRetryDecision.NoPolicy("No retry definition was available."));
            }

            if (context.RetryCount >= context.MaxRetries)
            {
                return ValueTask.FromResult(
                    AiRetryDecision.RetryExhausted(Key, "Maximum retry count exhausted."));
            }

            var delay = scheduler.ComputeDelay(context.Retry, context.RetryCount);

            return ValueTask.FromResult(
                AiRetryDecision.RetryScheduled(
                    Key,
                    delay,
                    "Transient failure retry scheduled."));
        }
    }
}