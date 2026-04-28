using System;
using System.Collections.Generic;
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
    ///
    /// PURPOSE:
    /// - Orchestrate compaction and eviction of steps.
    /// - Ensure safe externalization of step data.
    /// - Keep AiExecutionState bounded and efficient.
    /// - Delegate trigger and per-step decision enrichment to the decision service.
    ///
    /// IMPORTANT:
    /// - NEVER removes a step before payload persistence succeeds.
    /// - NEVER loses step existence metadata.
    /// - Writes an external archived-step index before eviction.
    /// - Returns only operations that were successfully applied.
    /// </summary>
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
                _metrics.RecordEvaluation(
                    mode,
                    totalStepsBefore,
                    0,
                    0);

                _metrics.RecordCompleted(
                    mode,
                    totalStepsBefore,
                    state.Steps.Count);

                return AiExecutionRetentionApplyResult.Empty;
            }

            var policy = _resolver.Resolve(mode);
            var plan = await policy.EvaluateAsync(state, cancellationToken)
                .ConfigureAwait(false);

            var stepsToCompact = new HashSet<string>(plan.StepsToCompact);
            var stepsToEvict = new HashSet<string>(plan.StepsToEvict);

            _decisionService.EnrichPlan(
                state,
                stepsToCompact,
                stepsToEvict,
                retentionDecision.TriggerContext);

            _metrics.RecordEvaluation(
                mode,
                totalStepsBefore,
                stepsToCompact.Count,
                stepsToEvict.Count);

            if (stepsToCompact.Count == 0 && stepsToEvict.Count == 0)
            {
                _metrics.RecordCompleted(
                    mode,
                    totalStepsBefore,
                    state.Steps.Count);

                return AiExecutionRetentionApplyResult.Empty;
            }

            var compactedSteps = new List<string>();
            var evictedSteps = new List<string>();

            foreach (var stepName in stepsToCompact)
            {
                if (stepsToEvict.Contains(stepName))
                {
                    continue;
                }

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