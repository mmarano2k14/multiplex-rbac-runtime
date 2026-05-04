using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry.Policies
{
    /// <summary>
    /// Default retry policy handling transient failures.
    /// </summary>
    /// <remarks>
    /// This policy evaluates whether a failure is transient and can be retried.
    /// It does not decide the retry scheduling, which is handled by the retry engine.
    /// </remarks>
    [AiPolicy("retry.transient.default", Kind = AiPolicyKind.Retry)]
    public sealed class DefaultTransientRetryPolicy : IAiPolicy
    {
        /// <inheritdoc />
        public string Key => "retry.transient.default";

        /// <inheritdoc />
        public AiPolicyKind Kind => AiPolicyKind.Retry;

        /// <inheritdoc />
        public Task<AiPolicyResult> ExecuteAsync(
            object context,
            CancellationToken cancellationToken = default)
        {
            var retryContext = (AiRetryContext)context;

            if (retryContext.RetryCount >= retryContext.MaxRetries)
            {
                return Task.FromResult(
                    AiPolicyResult.Block("Retry budget exhausted."));
            }

            var outcome = new AiRetryPolicyOutcome
            {
                IsRetryable = true,
                Reason = "Transient failure"
            };

            return Task.FromResult<AiPolicyResult>(AiPolicyResult.Retry<AiRetryPolicyOutcome>(outcome));
        }
    }
}