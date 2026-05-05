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
    /// PURPOSE:
    /// - Resolve retention configuration from merged pipeline and step configuration.
    /// - Execute retention policies.
    /// - Aggregate deterministic policy decisions.
    /// - Apply physical compaction and eviction through dedicated services.
    ///
    /// DESIGN:
    /// - Fully config-driven.
    /// - Safe by default.
    /// - Policy execution is decision-only.
    /// - Physical mutations are delegated to compaction and eviction services.
    ///
    /// IMPORTANT:
    /// - Missing retention configuration results in no-op behavior.
    /// - Disabled retention results in no-op behavior.
    /// - Missing or unresolved policies result in no-op behavior.
    /// - If a step is selected for both compaction and eviction, compaction is applied first,
    ///   then eviction is applied. This ensures all policy decisions are respected.
    /// </remarks>
    [AiPolicyEngine(AiPolicyKind.Retention)]
    public sealed class DefaultAiRetentionEngine : AiPolicyEngine, IAiRetentionEngine
    {
        private readonly IAiRetentionCompactionService _compactionService;
        private readonly IAiRetentionEvictionService _evictionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetentionEngine"/> class.
        /// </summary>
        /// <param name="policyRegistry">The policy registry used to resolve retention policies.</param>
        /// <param name="stepContext">The current step execution context.</param>
        /// <param name="observability">The runtime observability facade.</param>
        /// <param name="compactionService">The service used to apply retention compaction.</param>
        /// <param name="evictionService">The service used to apply safe retention eviction.</param>
        public DefaultAiRetentionEngine(
            IAiPolicyRegistry policyRegistry,
            AiStepExecutionContext stepContext,
            IAiRuntimeObservability observability,
            IAiRetentionCompactionService compactionService,
            IAiRetentionEvictionService evictionService)
            : base(policyRegistry, stepContext, observability)
        {
            _compactionService = compactionService ?? throw new ArgumentNullException(nameof(compactionService));
            _evictionService = evictionService ?? throw new ArgumentNullException(nameof(evictionService));
        }

        /// <inheritdoc />
        public override AiPolicyKind Kind => AiPolicyKind.Retention;

        /// <inheritdoc />
        public async Task<AiRetentionDecision> DecideAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.ExecutionState);

            var definition = await ResolveRetentionDefinitionAsync(cancellationToken)
                .ConfigureAwait(false);

            if (definition is null)
            {
                return AiRetentionDecision.None("Retention configuration was not found.");
            }

            if (!definition.Enabled)
            {
                return AiRetentionDecision.None("Retention is disabled.");
            }

            if (definition.Trigger.Enabled &&
                !ShouldRun(context.ExecutionState, definition.Trigger))
            {
                return AiRetentionDecision.None("Retention trigger thresholds were not reached.");
            }

            if (definition.Policies.Count == 0)
            {
                return AiRetentionDecision.None("No retention policies were configured.");
            }

            var policies = ResolvePolicies(
                definition.Policies,
                AiPolicyKind.Retention);

            if (policies.Count == 0)
            {
                return AiRetentionDecision.None("No retention policies were resolved.");
            }

            var policyContext = CreatePolicyContext(
                context,
                definition.Trigger);

            var results = await ExecutePoliciesAsync(
                    policyContext,
                    policies,
                    cancellationToken)
                .ConfigureAwait(false);

            return AggregateDecisions(results);
        }

        /// <inheritdoc />
        public async Task<AiRetentionApplyResult> ApplyAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.ExecutionState);

            var definition = await ResolveRetentionDefinitionAsync(cancellationToken)
                .ConfigureAwait(false);

            if (definition is null)
            {
                return AiRetentionApplyResult.Empty(
                    AiRetentionDecision.None("Retention configuration was not found."));
            }

            var decision = await DecideAsync(
                    context,
                    cancellationToken)
                .ConfigureAwait(false);

            if (decision.Kind == AiRetentionDecisionKind.None)
            {
                return AiRetentionApplyResult.Empty(decision);
            }

            var stepsToCompact = new HashSet<string>(
                decision.StepsToCompact ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            var stepsToEvict = new HashSet<string>(
                decision.StepsToEvict ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            if (stepsToCompact.Count == 0 && stepsToEvict.Count == 0)
            {
                return AiRetentionApplyResult.Empty(decision);
            }

            var compactedSteps = await _compactionService.CompactAsync(
                    context.ExecutionState,
                    stepsToCompact,
                    cancellationToken)
                .ConfigureAwait(false);

            var evictedSteps = await _evictionService.EvictAsync(
                    context.ExecutionState,
                    stepsToEvict,
                    definition.ArchiveReason,
                    cancellationToken)
                .ConfigureAwait(false);

            return new AiRetentionApplyResult
            {
                Decision = decision,
                CompactedSteps = compactedSteps,
                EvictedSteps = evictedSteps
            };
        }

        /// <summary>
        /// Resolves the retention configuration from merged pipeline and step configuration.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The resolved retention definition, or <see langword="null"/> when missing.</returns>
        private async Task<AiRetentionPolicyDefinition?> ResolveRetentionDefinitionAsync(
            CancellationToken cancellationToken)
        {
            return await ResolvePolicyDefinitionAsync<AiRetentionPolicyDefinition>(
                    "retention",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the policy evaluation context using the resolved trigger definition.
        /// </summary>
        /// <param name="source">The original retention context.</param>
        /// <param name="trigger">The resolved retention trigger definition.</param>
        /// <returns>A retention context enriched with resolved trigger configuration.</returns>
        private static AiRetentionContext CreatePolicyContext(
            AiRetentionContext source,
            AiRetentionTriggerDefinition trigger)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(trigger);

            return new AiRetentionContext
            {
                ExecutionId = source.ExecutionId,
                ExecutionState = source.ExecutionState,
                Trigger = trigger,
                UtcNow = source.UtcNow,
                Metadata = source.Metadata
            };
        }

        /// <summary>
        /// Determines whether retention should run based on configured trigger thresholds.
        /// </summary>
        /// <remarks>
        /// Global thresholds such as total step count and completed step count are evaluated here.
        /// Per-step inline payload pressure is also checked by looking at
        /// <see cref="AiStepState.InlinePayloadSizeBytes"/>.
        /// </remarks>
        /// <param name="state">The execution state to inspect.</param>
        /// <param name="trigger">The trigger definition containing threshold values.</param>
        /// <returns><c>true</c> when retention should run; otherwise, <c>false</c>.</returns>
        private static bool ShouldRun(
            AiExecutionState state,
            AiRetentionTriggerDefinition trigger)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(trigger);

            if (state.Steps.Count > trigger.MaxStepsInState)
            {
                return true;
            }

            var completedStepsCount = state.Steps.Values.Count(step => step.IsCompleted);

            return completedStepsCount > trigger.MaxCompletedStepsInState ||
                   HasAnyStepExceedingInlinePayloadThreshold(state, trigger);
        }

        /// <summary>
        /// Determines whether any step exceeds the configured per-step inline payload threshold.
        /// </summary>
        /// <param name="state">The execution state containing step states.</param>
        /// <param name="trigger">The trigger definition containing the payload threshold.</param>
        /// <returns>
        /// <c>true</c> when at least one step has inline payload pressure above the threshold;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool HasAnyStepExceedingInlinePayloadThreshold(
            AiExecutionState state,
            AiRetentionTriggerDefinition trigger)
        {
            foreach (var step in state.Steps.Values)
            {
                if (step.InlinePayloadSizeBytes is null)
                {
                    continue;
                }

                if (step.InlinePayloadSizeBytes.Value > trigger.MaxInlinePayloadBytes)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Aggregates policy results into a single deterministic retention decision.
        /// </summary>
        /// <remarks>
        /// The aggregation preserves all policy decisions.
        /// A step may appear in both the compaction and eviction sets.
        /// In that case, the engine will compact it first and evict it afterwards.
        /// </remarks>
        /// <param name="results">The policy results produced by the policy engine.</param>
        /// <returns>The aggregated retention decision.</returns>
        private static AiRetentionDecision AggregateDecisions(
            IReadOnlyCollection<AiPolicyResult> results)
        {
            ArgumentNullException.ThrowIfNull(results);

            var decisions = results
                .OfType<AiPolicyResultGeneric<AiRetentionDecision>>()
                .Select(result => result.Data)
                .Where(decision => decision is not null)
                .Select(decision => decision!)
                .ToList();

            if (decisions.Count == 0)
            {
                return AiRetentionDecision.None("No retention policies produced a decision.");
            }

            var stepsToCompact = new HashSet<string>(StringComparer.Ordinal);
            var stepsToEvict = new HashSet<string>(StringComparer.Ordinal);
            var finalKind = AiRetentionDecisionKind.None;

            foreach (var decision in decisions)
            {
                foreach (var stepName in decision.StepsToCompact ?? Array.Empty<string>())
                {
                    stepsToCompact.Add(stepName);
                }

                foreach (var stepName in decision.StepsToEvict ?? Array.Empty<string>())
                {
                    stepsToEvict.Add(stepName);
                }

                finalKind = MergeDecisionKind(finalKind, decision.Kind);
            }

            if (stepsToCompact.Count == 0 && stepsToEvict.Count == 0)
            {
                return AiRetentionDecision.None("Aggregated retention decision did not select any steps.");
            }

            return new AiRetentionDecision
            {
                Kind = finalKind,
                StepsToCompact = stepsToCompact.ToArray(),
                StepsToEvict = stepsToEvict.ToArray(),
                Reason = "Aggregated retention policy decisions."
            };
        }

        /// <summary>
        /// Merges two retention decision kinds using deterministic precedence.
        /// </summary>
        /// <param name="current">The current aggregated decision kind.</param>
        /// <param name="next">The next policy decision kind.</param>
        /// <returns>The merged decision kind.</returns>
        private static AiRetentionDecisionKind MergeDecisionKind(
            AiRetentionDecisionKind current,
            AiRetentionDecisionKind next)
        {
            if (current == AiRetentionDecisionKind.Hybrid || next == AiRetentionDecisionKind.Hybrid)
            {
                return AiRetentionDecisionKind.Hybrid;
            }

            if (current == AiRetentionDecisionKind.Evict || next == AiRetentionDecisionKind.Evict)
            {
                return AiRetentionDecisionKind.Evict;
            }

            if (current == AiRetentionDecisionKind.Compact || next == AiRetentionDecisionKind.Compact)
            {
                return AiRetentionDecisionKind.Compact;
            }

            return AiRetentionDecisionKind.None;
        }
    }
}