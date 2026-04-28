using Multiplexed.Abstractions.AI.Execution.Retention.Models;

namespace Multiplexed.Abstractions.AI.Execution.Retention.Policies
{
    /// <summary>
    /// Defines a deterministic policy that recommends a retention action for a single step.
    ///
    /// PURPOSE:
    /// - Decide what should happen to a completed step during retention.
    /// - Keep retention decision rules composable.
    ///
    /// IMPORTANT:
    /// - Must be deterministic.
    /// - Must be fast.
    /// - Must not perform I/O.
    /// - Must not mutate execution state.
    /// </summary>
    public interface IAiExecutionRetentionDecisionPolicy
    {
        /// <summary>
        /// Evaluates the retention decision for the specified step context.
        /// </summary>
        /// <param name="context">The retention decision context.</param>
        /// <returns>The recommended retention decision.</returns>
        AiExecutionRetentionDecision Evaluate(AiExecutionRetentionDecisionContext context);
    }
}