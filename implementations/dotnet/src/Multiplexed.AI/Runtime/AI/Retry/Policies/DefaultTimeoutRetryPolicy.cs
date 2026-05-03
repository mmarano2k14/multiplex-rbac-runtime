using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry.Policies
{
    /// <summary>
    /// Retry policy handling timeout-related failures.
    /// </summary>
    /// <remarks>
    /// This policy only classifies timeout-related failures as retryable.
    /// It does not compute the retry delay; delay calculation remains the
    /// responsibility of the retry engine and the resolved retry configuration.
    /// </remarks>
    [AiPolicy("retry.timeout.default", Kind = AiPolicyKind.Retry)]
    public sealed class DefaultTimeoutRetryPolicy : IAiPolicy
    {
        /// <inheritdoc />
        public string Key => "retry.timeout.default";

        /// <inheritdoc />
        public AiPolicyKind Kind => AiPolicyKind.Retry;

        /// <inheritdoc />
        public Task<AiPolicyResult> ExecuteAsync(
            object context,
            CancellationToken cancellationToken = default)
        {
            var retryContext = (AiRetryContext)context;

            if (retryContext.Exception is TimeoutException ||
                retryContext.Exception is TaskCanceledException)
            {
                var outcome = new AiRetryPolicyOutcome
                {
                    IsRetryable = true,
                    Reason = "Timeout detected.",
                    SuggestedDelay = null
                };

                return Task.FromResult<AiPolicyResult>(
                    AiPolicyResult.Retry(outcome));
            }

            return Task.FromResult<AiPolicyResult>(
                AiPolicyResult.Success("Not a timeout failure."));
        }
    }
}