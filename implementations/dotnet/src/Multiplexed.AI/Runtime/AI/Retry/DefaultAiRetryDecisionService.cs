using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Produces the final retry decision for failed AI steps.
    /// </summary>
    /// <remarks>
    /// The decision service coordinates retry classification, retry budget checks,
    /// and configured retry policies to produce a single deterministic decision
    /// that can later be applied atomically by the execution store.
    /// </remarks>
    public sealed class DefaultAiRetryDecisionService : IAiRetryDecisionService
    {
        private readonly IAiRetryClassifier classifier;
        private readonly IAiRetryPolicyResolver policyResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetryDecisionService"/> class.
        /// </summary>
        /// <param name="classifier">The classifier used to determine whether the failure can be retried.</param>
        /// <param name="policyResolver">The resolver used to load retry policies configured on the failed step.</param>
        public DefaultAiRetryDecisionService(
            IAiRetryClassifier classifier,
            IAiRetryPolicyResolver policyResolver)
        {
            this.classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            this.policyResolver = policyResolver ?? throw new ArgumentNullException(nameof(policyResolver));
        }

        /// <inheritdoc />
        public async ValueTask<AiRetryDecision> DecideAsync(
            AiRetryContext context,
            CancellationToken cancellationToken = default)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Retry is null || context.Retry.Policies.Count == 0)
            {
                return AiRetryDecision.NoPolicy("No retry policy was configured for the failed step.");
            }

            if (!classifier.IsRetryable(context))
            {
                return AiRetryDecision.NonRetryable(reason: "The failure was classified as non-retryable.");
            }

            if (context.RetryCount >= context.MaxRetries)
            {
                return AiRetryDecision.RetryExhausted(reason: "The retry budget has been exhausted.");
            }

            foreach (var policy in policyResolver.ResolveMany(context.Retry.Policies))
            {
                var decision = await policy.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);

                if (decision.Kind != AiRetryDecisionKind.NoPolicy)
                {
                    return decision;
                }
            }

            return AiRetryDecision.NoPolicy("No configured retry policy produced an applicable decision.");
        }
    }
}