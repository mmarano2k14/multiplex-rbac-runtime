namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Represents the result of applying a retention decision.
    /// </summary>
    public sealed class AiRetentionApplyResult
    {
        /// <summary>
        /// Gets the retention decision that was applied.
        /// </summary>
        public AiRetentionDecision Decision { get; init; } = AiRetentionDecision.None();

        /// <summary>
        /// Gets the step names that were successfully compacted.
        /// </summary>
        public IReadOnlyCollection<string> CompactedSteps { get; init; } = [];

        /// <summary>
        /// Gets the step names that were successfully evicted.
        /// </summary>
        public IReadOnlyCollection<string> EvictedSteps { get; init; } = [];

        /// <summary>
        /// Gets a value indicating whether no retention action was applied.
        /// </summary>
        public bool IsEmpty => CompactedSteps.Count == 0 && EvictedSteps.Count == 0;

        /// <summary>
        /// Creates an empty retention application result.
        /// </summary>
        /// <param name="decision">The retention decision associated with the empty result.</param>
        /// <returns>An empty retention application result.</returns>
        public static AiRetentionApplyResult Empty(AiRetentionDecision? decision = null)
        {
            return new AiRetentionApplyResult
            {
                Decision = decision ?? AiRetentionDecision.None()
            };
        }
    }
}