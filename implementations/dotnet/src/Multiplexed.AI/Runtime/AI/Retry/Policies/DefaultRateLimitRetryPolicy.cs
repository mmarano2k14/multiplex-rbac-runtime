using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry.Policies
{
    /// <summary>
    /// Retry policy handling rate-limiting and throttling scenarios.
    /// </summary>
    /// <remarks>
    /// This policy detects rate limit errors such as HTTP 429 or service throttling
    /// conditions and suggests retry with a longer delay.
    /// </remarks>
    [AiPolicy("retry.rate-limit.default", Kind = AiPolicyKind.Retry)]
    public sealed class DefaultRateLimitRetryPolicy : IAiPolicy
    {
        /// <inheritdoc />
        public string Key => "retry.rate-limit.default";

        /// <inheritdoc />
        public AiPolicyKind Kind => AiPolicyKind.Retry;

        /// <inheritdoc />
        public Task<AiPolicyResult> ExecuteAsync(
            object context,
            CancellationToken cancellationToken = default)
        {
            var retryContext = (AiRetryContext)context;

            var exception = retryContext.Exception;
            var reason = retryContext.FailureReason;

            var isRateLimit =
                (exception?.Message?.Contains("429") == true) ||
                (reason?.Contains("rate", StringComparison.OrdinalIgnoreCase) == true) ||
                (reason?.Contains("throttle", StringComparison.OrdinalIgnoreCase) == true);

            if (isRateLimit)
            {
                var outcome = new AiRetryPolicyOutcome
                {
                    IsRetryable = true,
                    Reason = "Rate limit detected.",
                    SuggestedDelay = TimeSpan.FromMilliseconds(2000)
                };

                return Task.FromResult<AiPolicyResult>(
                    AiPolicyResult.Retry<AiRetryPolicyOutcome>(outcome));
            }

            return Task.FromResult<AiPolicyResult>(
                AiPolicyResult.Success("Not a rate limit failure."));
        }
    }
}