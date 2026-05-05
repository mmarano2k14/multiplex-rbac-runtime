namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Represents the result produced by retention policy evaluation.
    /// </summary>
    public sealed class AiRetentionDecision
    {
        /// <summary>
        /// Gets the retention decision kind.
        /// </summary>
        public AiRetentionDecisionKind Kind { get; init; }

        /// <summary>
        /// Gets the step names selected for compaction.
        /// </summary>
        public IReadOnlyCollection<string> StepsToCompact { get; init; } = [];

        /// <summary>
        /// Gets the step names selected for eviction.
        /// </summary>
        public IReadOnlyCollection<string> StepsToEvict { get; init; } = [];

        /// <summary>
        /// Gets the reason associated with the decision.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Creates a no-operation retention decision.
        /// </summary>
        /// <param name="reason">The optional reason why no retention action should be performed.</param>
        /// <returns>A no-operation retention decision.</returns>
        public static AiRetentionDecision None(string? reason = null)
        {
            return new AiRetentionDecision
            {
                Kind = AiRetentionDecisionKind.None,
                Reason = reason
            };
        }
    }
}