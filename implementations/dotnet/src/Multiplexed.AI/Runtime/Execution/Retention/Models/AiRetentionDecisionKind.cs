namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Defines the possible outcomes of a retention policy evaluation.
    /// </summary>
    public enum AiRetentionDecisionKind
    {
        /// <summary>
        /// No retention action should be performed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Eligible payloads should be compacted.
        /// </summary>
        Compact = 1,

        /// <summary>
        /// Eligible hot-state entries should be evicted.
        /// </summary>
        Evict = 2,

        /// <summary>
        /// Eligible payloads should be compacted first, then eligible hot-state entries should be evicted.
        /// </summary>
        Hybrid = 3
    }
}