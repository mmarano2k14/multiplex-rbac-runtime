using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Policies
{
    /// <summary>
    /// Retention policy responsible for selecting terminal steps eligible for compaction.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Identify steps whose inline payload can be compacted to reduce memory footprint.
    /// - Target terminal steps (Completed or Failed) as safe candidates.
    ///
    /// BEHAVIOR:
    /// - Evaluates the execution state from <see cref="AiRetentionContext"/>.
    /// - Selects terminal steps whose inline payload size exceeds the configured threshold.
    /// - Produces a <see cref="AiRetentionDecision"/> containing the steps to compact.
    ///
    /// DESIGN:
    /// - This policy is decision-only.
    /// - It does not mutate execution state.
    /// - It does not perform payload compaction or persistence.
    /// - Physical compaction is delegated to <c>IAiRetentionCompactionService</c>.
    ///
    /// IMPORTANT:
    /// - Deterministic: same input state → same output decision.
    /// - Safe to execute multiple times (idempotent selection).
    /// - Uses <see cref="AiStepState.InlinePayloadSizeBytes"/> to avoid runtime serialization cost.
    /// - If trigger is disabled, all terminal steps are considered eligible.
    /// </remarks>
    [AiPolicy("retention.compact.terminal", Kind = AiPolicyKind.Retention)]
    public sealed class CompactAiRetentionPolicy : AiPolicyBase<AiRetentionContext>
    {
        /// <summary>
        /// Gets the unique key identifying the policy.
        /// </summary>
        public override string Key => "retention.compact.terminal";

        /// <summary>
        /// Gets the policy kind.
        /// </summary>
        public override AiPolicyKind Kind => AiPolicyKind.Retention;

        /// <inheritdoc />
        public override Task<AiPolicyResult> ExecuteAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.ExecutionState);

            var trigger = context.Trigger ?? new AiRetentionTriggerDefinition();

            var stepsToCompact = context.ExecutionState.Steps
                .Where(step => IsTerminal(step.Value))
                .Where(step => ShouldCompact(step.Value, trigger))
                .Select(step => step.Key)
                .ToArray();

            var result = AiPolicyResult.Success(
                new AiRetentionDecision
                {
                    Kind = AiRetentionDecisionKind.Compact,
                    StepsToCompact = stepsToCompact,
                    Reason = "Terminal steps exceeding inline payload threshold selected for compaction."
                });

            return Task.FromResult<AiPolicyResult>(result);
        }

        /// <summary>
        /// Determines whether the specified step should be compacted based on trigger configuration.
        /// </summary>
        /// <param name="step">The step state to evaluate.</param>
        /// <param name="trigger">The retention trigger definition.</param>
        /// <returns>
        /// <c>true</c> if the step should be compacted; otherwise, <c>false</c>.
        /// </returns>
        private static bool ShouldCompact(
            AiStepState step,
            AiRetentionTriggerDefinition trigger)
        {
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(trigger);

            // If trigger is disabled, compact all terminal steps
            if (!trigger.Enabled)
            {
                return true;
            }

            // Only compact if payload size exceeds threshold
            return step.InlinePayloadSizeBytes.HasValue &&
                   step.InlinePayloadSizeBytes.Value > trigger.MaxInlinePayloadBytes;
        }

        /// <summary>
        /// Determines whether the specified step is in a terminal state.
        /// </summary>
        /// <param name="step">The step state to evaluate.</param>
        /// <returns>
        /// <c>true</c> if the step is completed or failed; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsTerminal(AiStepState step)
        {
            return step.Status == AiStepExecutionStatus.Completed ||
                   step.Status == AiStepExecutionStatus.Failed;
        }
    }
}