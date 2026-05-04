using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Defines a policy capable of evaluating retry behavior for a failed AI step.
    /// </summary>
    /// <remarks>
    /// Retry policies are resolved by key from step retry configuration and evaluated
    /// by the retry decision service. Implementations should be deterministic and should
    /// avoid mutating execution state directly.
    /// </remarks>
    public interface IAiRetryPolicy : IAiPolicy
    {
        /// <summary>
        /// Evaluates whether a failed step should be retried or failed permanently.
        /// </summary>
        /// <param name="context">
        /// The retry context describing the failed step and its retry configuration.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to observe cancellation requests.
        /// </param>
        /// <returns>
        /// A retry decision describing whether to schedule a retry, stop retrying,
        /// or fail immediately as non-retryable.
        /// </returns>
        ValueTask<AiRetryDecision> EvaluateAsync(
            AiRetryContext context,
            CancellationToken cancellationToken = default);
    }
}