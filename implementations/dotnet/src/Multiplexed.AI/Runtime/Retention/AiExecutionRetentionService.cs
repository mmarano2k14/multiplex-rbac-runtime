using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Decisions;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;

namespace Multiplexed.AI.Runtime.Retention
{
    /// <summary>
    /// Applies execution state retention based on a selected policy.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Orchestrates compaction and eviction of execution steps.
    /// - Ensures safe persistence of step payloads before eviction.
    /// - Keeps <see cref="AiExecutionState"/> bounded in memory.
    ///
    /// DESIGN PRINCIPLES:
    /// - Policy is the source of truth for eviction.
    /// - Decision service may enrich compaction only.
    /// - Eviction must never be expanded after policy evaluation.
    ///
    /// SAFETY:
    /// - NEVER removes a step before payload persistence succeeds.
    /// - NEVER loses step existence metadata.
    /// - ALWAYS writes archived index before eviction.
    /// - ALWAYS returns only successfully applied operations.
    /// </remarks>
    public sealed class AiExecutionRetentionService : IAiExecutionRetentionService
    {
        private readonly IAiExecutionRetentionPolicyResolver _resolver;
        private readonly IAiStepPayloadStore _stepPayloadStore;
        private readonly IAiStepPayloadIndexStore _stepPayloadIndexStore;
        private readonly IAiStepResultPayloadCompactor _compactor;
        private readonly IAiExecutionRetentionServiceMetrics _metrics;
        private readonly IAiExecutionRetentionDecisionService _decisionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionRetentionService"/> class.
        /// </summary>
        public AiExecutionRetentionService(
            IAiExecutionRetentionPolicyResolver resolver,
            IAiStepPayloadStore stepPayloadStore,
            IAiStepPayloadIndexStore stepPayloadIndexStore,
            IAiStepResultPayloadCompactor compactor,
            IAiExecutionRetentionServiceMetrics metrics,
            IAiExecutionRetentionDecisionService decisionService)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _stepPayloadStore = stepPayloadStore ?? throw new ArgumentNullException(nameof(stepPayloadStore));
            _stepPayloadIndexStore = stepPayloadIndexStore ?? throw new ArgumentNullException(nameof(stepPayloadIndexStore));
            _compactor = compactor ?? throw new ArgumentNullException(nameof(compactor));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _decisionService = decisionService ?? throw new ArgumentNullException(nameof(decisionService));
        }

        /// <inheritdoc />
        public async ValueTask<AiExecutionRetentionApplyResult> ApplyAsync(
            AiExecutionState state,
            AiExecutionRetentionMode mode,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            var totalStepsBefore = state.Steps.Count;

            var retentionDecision = _decisionService.Evaluate(state);

            if (!retentionDecision.ShouldRun)
            {
                _metrics.RecordEvaluation(mode, totalStepsBefore, 0, 0);
                _metrics.RecordCompleted(mode, totalStepsBefore, state.Steps.Count);
                return AiExecutionRetentionApplyResult.Empty;
            }

            var policy = _resolver.Resolve(mode);
            var plan = await policy.EvaluateAsync(state, cancellationToken)
                .ConfigureAwait(false);

            // 🔥 SOURCE OF TRUTH (IMPORTANT)
            var policyEvictionSet = new HashSet<string>(
                plan.StepsToEvict ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            var stepsToEvict = new HashSet<string>(
                policyEvictionSet,
                StringComparer.Ordinal);

            var stepsToCompact = new HashSet<string>(
                plan.StepsToCompact ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            // Decision enrichment (compaction only)
            _decisionService.EnrichPlan(
                state,
                stepsToCompact,
                stepsToEvict,
                retentionDecision.TriggerContext);

            // Prevent enrichment from expanding eviction
            stepsToEvict.IntersectWith(policyEvictionSet);

            // Avoid compacting steps that will be evicted
            stepsToCompact.ExceptWith(stepsToEvict);

            _metrics.RecordEvaluation(
                mode,
                totalStepsBefore,
                stepsToCompact.Count,
                stepsToEvict.Count);

            if (stepsToCompact.Count == 0 && stepsToEvict.Count == 0)
            {
                _metrics.RecordCompleted(mode, totalStepsBefore, state.Steps.Count);
                return AiExecutionRetentionApplyResult.Empty;
            }

            var compactedSteps = new List<string>();
            var evictedSteps = new List<string>();

            // ----------------------------
            // COMPACTION
            // ----------------------------
            foreach (var stepName in stepsToCompact)
            {
                if (!state.Steps.TryGetValue(stepName, out var step))
                {
                    continue;
                }

                if (step.Result is null)
                {
                    continue;
                }

                await _compactor.CompactAsync(
                        step.Result,
                        cancellationToken)
                    .ConfigureAwait(false);

                compactedSteps.Add(stepName);
                _metrics.RecordCompacted(stepName);
            }

            // ----------------------------
            // EVICTION
            // ----------------------------
            foreach (var stepName in stepsToEvict)
            {
                if (!state.Steps.TryGetValue(stepName, out var step))
                {
                    continue;
                }

                var payload = await _stepPayloadStore.SaveStepAsync(
                        state.ExecutionId,
                        stepName,
                        step,
                        cancellationToken)
                    .ConfigureAwait(false);

                await _stepPayloadIndexStore.MarkArchivedAsync(
                        new AiArchivedStepPayloadIndex
                        {
                            ExecutionId = state.ExecutionId,
                            StepName = stepName,
                            Status = step.Status,
                            Payload = payload,
                            ArchivedAtUtc = DateTime.UtcNow,
                            Reason = "retention"
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                if (state.Steps.Remove(stepName))
                {
                    evictedSteps.Add(stepName);
                    _metrics.RecordEvicted(stepName);
                }
            }

            _metrics.RecordCompleted(
                mode,
                totalStepsBefore,
                state.Steps.Count);

            return new AiExecutionRetentionApplyResult
            {
                CompactedSteps = compactedSteps,
                EvictedSteps = evictedSteps
            };
        }
    }
}