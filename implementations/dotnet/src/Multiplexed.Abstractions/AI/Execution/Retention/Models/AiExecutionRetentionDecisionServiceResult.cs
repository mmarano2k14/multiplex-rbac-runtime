namespace Multiplexed.Abstractions.AI.Execution.Retention.Models
{
    /// <summary>
    /// Represents the result of evaluating whether retention should run.
    ///
    /// PURPOSE:
    /// - Carry the trigger decision.
    /// - Reuse the computed trigger context for later decision enrichment.
    ///
    /// IMPORTANT:
    /// - This is decision metadata only.
    /// - It must not apply retention.
    /// </summary>
    public sealed class AiExecutionRetentionDecisionServiceResult
    {
        /// <summary>
        /// Gets whether retention should run.
        /// </summary>
        public bool ShouldRun { get; init; }

        /// <summary>
        /// Gets the trigger context used to evaluate the decision.
        /// </summary>
        public required AiExecutionRetentionTriggerContext TriggerContext { get; init; }
    }
}