using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Produces the final retry decision for a failed AI step.
    /// </summary>
    /// <remarks>
    /// The decision service coordinates configured retry policies and produces one
    /// deterministic decision that can be applied atomically by the execution store.
    /// </remarks>
    public interface IAiRetryDecisionService
    {
        /// <summary>
        /// Determines the retry outcome for a failed AI step.
        /// </summary>
        /// <param name="context">
        /// The retry context describing the failed step and its retry configuration.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to observe cancellation requests.
        /// </param>
        /// <returns>
        /// A retry decision that instructs the runtime to schedule a retry or fail the step permanently.
        /// </returns>
        ValueTask<AiRetryDecision> DecideAsync(
            AiRetryContext context,
            CancellationToken cancellationToken = default);
    }
}