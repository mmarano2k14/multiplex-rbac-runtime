using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Default implementation of the retention policy engine.
    /// </summary>
    /// <remarks>
    /// This engine is responsible only for retention decision evaluation.
    /// Physical retention actions such as compaction, archive persistence, and hot-state eviction
    /// remain owned by the retention service.
    /// </remarks>
    [AiPolicyEngine(AiPolicyKind.Retention)]
    public sealed class DefaultAiRetentionEngine : AiPolicyEngine, IAiRetentionEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetentionEngine"/> class.
        /// </summary>
        /// <param name="policyRegistry">The policy registry used to resolve retention policies.</param>
        /// <param name="stepContext">The current step execution context.</param>
        /// <param name="observability">The runtime observability facade.</param>
        public DefaultAiRetentionEngine(
            IAiPolicyRegistry policyRegistry,
            AiStepExecutionContext stepContext,
            IAiRuntimeObservability observability)
            : base(policyRegistry, stepContext, observability)
        {
        }

        /// <inheritdoc />
        public override AiPolicyKind Kind => AiPolicyKind.Retention;

        /// <inheritdoc />
        public async Task<AiRetentionDecision> DecideAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var definition = await ResolvePolicyDefinitionAsync<AiRetentionPolicyDefinition>(
                    "retention",
                    cancellationToken)
                .ConfigureAwait(false);

            if (definition is null || definition.Policies.Count == 0)
            {
                return AiRetentionDecision.None("No retention policy configuration was found.");
            }

            var policies = ResolvePolicies(
                definition.Policies,
                AiPolicyKind.Retention);

            var results = await ExecutePoliciesAsync(
                context,
                policies,
                cancellationToken)
            .ConfigureAwait(false);
            var decision = results
                .OfType<AiPolicyResultGeneric<AiRetentionDecision>>()
                .Select(result => result.Data)
                .FirstOrDefault();

            return decision ?? AiRetentionDecision.None("Retention policies did not produce a decision.");
        }
    }
}