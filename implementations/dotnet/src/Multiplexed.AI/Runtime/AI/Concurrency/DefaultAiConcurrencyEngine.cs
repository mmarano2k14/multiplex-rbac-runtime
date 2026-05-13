using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Concurrency
{
    /// <summary>
    /// Default implementation of the concurrency policy engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This engine is decision-only.
    /// </para>
    ///
    /// <para>
    /// It resolves concurrency configuration from the current step state, executes configured
    /// concurrency policies, and computes whether a step may proceed to distributed slot acquisition.
    /// </para>
    ///
    /// <para>
    /// This engine does not acquire Redis leases. Distributed admission remains the responsibility
    /// of <see cref="IAiConcurrencyGate"/>.
    /// </para>
    ///
    /// <para>
    /// The engine follows the same policy-engine pattern as the retry engine:
    /// resolve the step-scoped definition, resolve policies by kind, execute policies, then convert
    /// policy results into a domain decision.
    /// </para>
    /// </remarks>
    [AiPolicyEngine(AiPolicyKind.Concurrency)]
    public sealed class DefaultAiConcurrencyEngine : AiPolicyEngine, IAiConcurrencyEngine
    {
        private static readonly IAiConcurrencyDefinitionResolver DefinitionResolver =
            new DefaultAiConcurrencyDefinitionResolver();

        private static readonly AiConcurrencyDefinition DefaultConcurrencyDefinition = new()
        {
            Enabled = false,
            Policies = new List<AiConfiguredPolicyDefinition>(),
            DefaultRetryAfterMs = 250,
            LeaseSeconds = 300,
            MaxJitterMs = 100
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiConcurrencyEngine"/> class.
        /// </summary>
        /// <param name="policyRegistry">
        /// The policy registry used to resolve concurrency policies.
        /// </param>
        /// <param name="stepContext">
        /// The current step execution context.
        /// </param>
        /// <param name="obs">
        /// The runtime observability service.
        /// </param>
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

            var evaluation = await EvaluateAsync(
                    context,
                    cancellationToken)
                .ConfigureAwait(false);

            return evaluation.Decision;
        }

        /// <inheritdoc />
        public async Task<AiConcurrencyEvaluation> EvaluateAsync(
            AiConcurrencyContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var definition = await ResolveConcurrencyDefinitionAsync(
                    cancellationToken)
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
                definition.Policies.GetPolicyNames(),
                AiPolicyKind.Concurrency);

            var results = await ExecutePoliciesAsync(
                    context,
                    policies,
                    cancellationToken)
                .ConfigureAwait(false);

            var decision = CreateDecision(
                definition,
                results);

            return new AiConcurrencyEvaluation
            {
                Definition = definition,
                Decision = decision
            };
        }

        /// <summary>
        /// Resolves concurrency configuration from the current step context and falls back
        /// to the default concurrency definition when no configuration is available.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// The resolved concurrency definition.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The resolver is intentionally used here instead of deserializing directly into
        /// <see cref="AiConcurrencyDefinition"/>.
        /// </para>
        ///
        /// <para>
        /// This preserves the resolver behavior added for concurrency:
        /// nullable raw config, policy-config defaults, direct config priority, and safe default
        /// application after merge.
        /// </para>
        /// </remarks>
        public Task<AiConcurrencyDefinition> ResolveConcurrencyDefinitionAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definition = DefinitionResolver.Resolve(
                StepContext.StepState);

            return Task.FromResult(definition ?? DefaultConcurrencyDefinition);
        }

        /// <summary>
        /// Converts policy execution results into a concurrency decision.
        /// </summary>
        /// <param name="definition">
        /// The resolved concurrency definition.
        /// </param>
        /// <param name="results">
        /// The executed policy results.
        /// </param>
        /// <returns>
        /// The computed concurrency decision.
        /// </returns>
        private static AiConcurrencyDecision CreateDecision(
            AiConcurrencyDefinition definition,
            IReadOnlyCollection<AiPolicyResult> results)
        {
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

            if (outcomes.Count == 0)
            {
                return AiConcurrencyDecision.Allow();
            }

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
    }
}