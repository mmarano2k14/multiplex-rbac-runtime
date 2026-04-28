namespace Multiplexed.Abstractions.AI.Execution.Retention.Models
{
    /// <summary>
    /// Represents the retention decision produced for a single execution step.
    ///
    /// PURPOSE:
    /// - Capture the recommended retention action.
    /// - Preserve the reason behind the decision for diagnostics and metrics.
    ///
    /// IMPORTANT:
    /// - This is immutable decision data.
    /// - It must not mutate execution state.
    /// </summary>
    public sealed class AiExecutionRetentionDecision
    {
        /// <summary>
        /// Gets the recommended retention action.
        /// </summary>
        public AiExecutionRetentionAction Action { get; init; } = AiExecutionRetentionAction.Keep;

        /// <summary>
        /// Gets the reason associated with the decision.
        /// </summary>
        public string Reason { get; init; } = "keep";
    }
}