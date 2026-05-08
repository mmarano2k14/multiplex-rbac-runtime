using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Concurrency
{
    /// <summary>
    /// Default implementation of the concurrency policy engine.
    /// </summary>
    /// <remarks>
    /// This engine is decision-only.
    /// It resolves concurrency configuration, executes concurrency policies,
    /// and computes whether a step may proceed to distributed slot acquisition.
    /// </remarks>
    [AiPolicyEngine(AiPolicyKind.Concurrency)]
    public sealed class DefaultAiConcurrencyEngine : AiPolicyEngine, IAiConcurrencyEngine
    {
        private static readonly AiConcurrencyDefinition DefaultConcurrencyDefinition = new()
        {
            Enabled = false,
            Policies = new List<string>(),
            DefaultRetryAfterMs = 250
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiConcurrencyEngine"/> class.
        /// </summary>
        /// <param name="policyRegistry">The policy registry used to resolve concurrency policies.</param>
        /// <param name="stepContext">The current step execution context.</param>
        /// <param name="obs">The runtime observability service.</param>
        public DefaultAiConcurrencyEngine(
            IAiPolicyRegistry policyRegistry,
            AiStepExecutionContext stepContext,
            IAiRuntimeObservability obs)
            : base(policyRegistry, stepContext, obs)
        {
        }

        /// <inheritdoc />
        public override AiPolicyKind Kind => AiPolicyKind.Concurrency;

        /// <inheritdoc />
        public async Task<AiConcurrencyDecision> DecideAsync(
            AiConcurrencyContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var definition = await ResolveConcurrencyDefinitionAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!definition.Enabled)
            {
                return AiConcurrencyDecision.Allow();
            }

            var policies = ResolvePolicies(
                definition.Policies,
                AiPolicyKind.Concurrency);

            var results = await ExecutePoliciesAsync(
                    context,
                    policies,
                    cancellationToken)
                .ConfigureAwait(false);

            if (results.Any(x => x.Kind == AiPolicyResultKind.Block))
            {
                return AiConcurrencyDecision.Deny(
                    "Blocked by concurrency policy.",
                    TimeSpan.FromMilliseconds(definition.DefaultRetryAfterMs));
            }

            var outcomes = results
                .OfType<AiPolicyResultGeneric<AiConcurrencyPolicyOutcome>>()
                .Select(x => x.Data)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            if (outcomes.Any(x => !x.IsAllowed))
            {
                var reason = outcomes
                    .Select(x => x.Reason)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                var retryAfter = outcomes
                    .Where(x => x.RetryAfter.HasValue)
                    .Select(x => x.RetryAfter!.Value)
                    .DefaultIfEmpty(TimeSpan.FromMilliseconds(definition.DefaultRetryAfterMs))
                    .Max();

                return AiConcurrencyDecision.Deny(
                    reason ?? "Concurrency policy denied execution.",
                    retryAfter);
            }

            return AiConcurrencyDecision.Allow();
        }

        /// <inheritdoc />
        public async Task<AiConcurrencyEvaluation> EvaluateAsync(
            AiConcurrencyContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var definition = await ResolveConcurrencyDefinitionAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!definition.Enabled)
            {
                return new AiConcurrencyEvaluation
                {
                    Definition = definition,
                    Decision = AiConcurrencyDecision.Allow()
                };
            }

            var policies = ResolvePolicies(
                definition.Policies,
                AiPolicyKind.Concurrency);

            var results = await ExecutePoliciesAsync(
                    context,
                    policies,
                    cancellationToken)
                .ConfigureAwait(false);

            if (results.Any(x => x.Kind == AiPolicyResultKind.Block))
            {
                return new AiConcurrencyEvaluation
                {
                    Definition = definition,
                    Decision = AiConcurrencyDecision.Deny(
                        "Blocked by concurrency policy.",
                        TimeSpan.FromMilliseconds(definition.DefaultRetryAfterMs))
                };
            }

            var outcomes = results
                .OfType<AiPolicyResultGeneric<AiConcurrencyPolicyOutcome>>()
                .Select(x => x.Data)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            if (outcomes.Any(x => !x.IsAllowed))
            {
                var reason = outcomes
                    .Select(x => x.Reason)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                var retryAfter = outcomes
                    .Where(x => x.RetryAfter.HasValue)
                    .Select(x => x.RetryAfter!.Value)
                    .DefaultIfEmpty(TimeSpan.FromMilliseconds(definition.DefaultRetryAfterMs))
                    .Max();

                return new AiConcurrencyEvaluation
                {
                    Definition = definition,
                    Decision = AiConcurrencyDecision.Deny(
                        reason ?? "Concurrency policy denied execution.",
                        retryAfter)
                };
            }

            return new AiConcurrencyEvaluation
            {
                Definition = definition,
                Decision = AiConcurrencyDecision.Allow()
            };
        }

        /// <summary>
        /// Resolves concurrency configuration from the current step context and falls back
        /// to the default concurrency definition when no configuration is available.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
        /// <returns>The resolved concurrency definition.</returns>
        public async Task<AiConcurrencyDefinition> ResolveConcurrencyDefinitionAsync(
            CancellationToken cancellationToken = default)
        {
            var definition = await ResolvePolicyDefinitionAsync<AiConcurrencyDefinition>(
                    "concurrency",
                    cancellationToken)
                .ConfigureAwait(false);

            return definition ?? DefaultConcurrencyDefinition;
        }
    }
}