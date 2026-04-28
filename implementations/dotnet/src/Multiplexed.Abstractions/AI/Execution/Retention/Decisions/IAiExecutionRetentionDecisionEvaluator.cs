using Multiplexed.Abstractions.AI.Execution.Retention.Models;

namespace Multiplexed.Abstractions.AI.Execution.Retention.Decisions
{
    /// <summary>
    /// Evaluates retention decisions by combining one or more retention decision policies.
    ///
    /// PURPOSE:
    /// - Aggregate policy recommendations into a single deterministic decision.
    /// - Keep decision orchestration separate from individual policy rules.
    ///
    /// IMPORTANT:
    /// - Must be deterministic.
    /// - Must preserve stable policy ordering.
    /// - Must not perform I/O.
    /// - Must not mutate execution state.
    /// </summary>
    public interface IAiExecutionRetentionDecisionEvaluator
    {
        /// <summary>
        /// Evaluates the final retention decision for the specified context.
        /// </summary>
        /// <param name="context">The retention decision context.</param>
        /// <returns>The final retention decision.</returns>
        AiExecutionRetentionDecision Evaluate(AiExecutionRetentionDecisionContext context);
    }
}