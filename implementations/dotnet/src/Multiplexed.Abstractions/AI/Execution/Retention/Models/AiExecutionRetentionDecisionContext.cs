using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Execution.Retention.Models
{
    /// <summary>
    /// Provides decision-time context for evaluating retention of a single step.
    ///
    /// PURPOSE:
    /// - Expose the current step and aggregate execution pressure.
    /// - Allow deterministic retention policies to decide keep, compact, or evict.
    ///
    /// IMPORTANT:
    /// - This context is read-only.
    /// - Policies must not mutate the execution state or step state.
    /// </summary>
    public sealed class AiExecutionRetentionDecisionContext
    {
        /// <summary>
        /// Gets the execution state currently being evaluated.
        /// </summary>
        public required AiExecutionState State { get; init; }

        /// <summary>
        /// Gets the name of the step currently being evaluated.
        /// </summary>
        public required string StepName { get; init; }

        /// <summary>
        /// Gets the step state currently being evaluated.
        /// </summary>
        public required AiStepState Step { get; init; }

        /// <summary>
        /// Gets the total number of steps currently present in the execution state.
        /// </summary>
        public int TotalStepsCount { get; init; }

        /// <summary>
        /// Gets the number of completed steps currently present in the execution state.
        /// </summary>
        public int CompletedStepsCount { get; init; }

        /// <summary>
        /// Gets the estimated size, in bytes, of inline payload data for the current step.
        /// </summary>
        public long StepInlinePayloadBytes { get; init; }
    }
}