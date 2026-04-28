using System;
using System.Collections.Generic;
using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Retention.Decisions;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Triggers;

namespace Multiplexed.AI.Runtime.Retention.Decisions
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionRetentionDecisionService"/>.
    ///
    /// PURPOSE:
    /// - Build trigger context from execution state.
    /// - Execute the retention trigger.
    /// - Enrich retention plans using per-step decision evaluation.
    ///
    /// IMPORTANT:
    /// - Does not apply retention.
    /// - Does not persist payloads.
    /// - Does not mutate execution state.
    /// </summary>
    public sealed class DefaultAiExecutionRetentionDecisionService : IAiExecutionRetentionDecisionService
    {
        private readonly IAiExecutionRetentionTrigger _trigger;
        private readonly IAiExecutionRetentionDecisionEvaluator _evaluator;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionRetentionDecisionService"/> class.
        /// </summary>
        public DefaultAiExecutionRetentionDecisionService(
            IAiExecutionRetentionTrigger trigger,
            IAiExecutionRetentionDecisionEvaluator evaluator)
        {
            _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <inheritdoc />
        public AiExecutionRetentionDecisionServiceResult Evaluate(
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            var context = BuildTriggerContext(state);

            return new AiExecutionRetentionDecisionServiceResult
            {
                ShouldRun = _trigger.ShouldRun(context),
                TriggerContext = context
            };
        }

        /// <inheritdoc />
        public void EnrichPlan(
            AiExecutionState state,
            ISet<string> stepsToCompact,
            ISet<string> stepsToEvict,
            AiExecutionRetentionTriggerContext triggerContext)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepsToCompact);
            ArgumentNullException.ThrowIfNull(stepsToEvict);
            ArgumentNullException.ThrowIfNull(triggerContext);

            foreach (var entry in state.Steps)
            {
                var stepName = entry.Key;
                var step = entry.Value;

                if (!step.IsCompleted)
                {
                    continue;
                }

                if (stepsToEvict.Contains(stepName))
                {
                    continue;
                }

                var decision = _evaluator.Evaluate(
                    new AiExecutionRetentionDecisionContext
                    {
                        State = state,
                        StepName = stepName,
                        Step = step,
                        TotalStepsCount = triggerContext.TotalStepsCount,
                        CompletedStepsCount = triggerContext.CompletedStepsCount,
                        StepInlinePayloadBytes =
                            EstimatePayloadBytes(step.Result?.Value) +
                            EstimatePayloadBytes(step.Result?.Data)
                    });

                if (decision.Action == AiExecutionRetentionAction.Compact)
                {
                    stepsToCompact.Add(stepName);
                }
            }
        }

        /// <summary>
        /// Builds a lightweight retention trigger context from the current execution state.
        /// </summary>
        private static AiExecutionRetentionTriggerContext BuildTriggerContext(AiExecutionState state)
        {
            var completedSteps = 0;
            var estimatedInlinePayloadBytes = 0L;

            foreach (var step in state.Steps.Values)
            {
                if (step.IsCompleted)
                {
                    completedSteps++;
                }

                if (step.Result is not null)
                {
                    estimatedInlinePayloadBytes += EstimatePayloadBytes(step.Result.Value);
                    estimatedInlinePayloadBytes += EstimatePayloadBytes(step.Result.Data);
                }
            }

            return new AiExecutionRetentionTriggerContext
            {
                TotalStepsCount = state.Steps.Count,
                CompletedStepsCount = completedSteps,
                EstimatedInlinePayloadBytes = estimatedInlinePayloadBytes
            };
        }

        /// <summary>
        /// Estimates the serialized size of an inline payload value.
        /// </summary>
        private static long EstimatePayloadBytes(object? value)
        {
            if (value is null)
            {
                return 0;
            }

            try
            {
                return JsonSerializer.SerializeToUtf8Bytes(value).LongLength;
            }
            catch
            {
                return 0;
            }
        }
    }
}