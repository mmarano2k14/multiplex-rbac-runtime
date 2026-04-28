namespace Multiplexed.Abstractions.AI.Execution.Retention.Models
{
    /// <summary>
    /// Represents the action recommended by a retention decision policy.
    ///
    /// PURPOSE:
    /// - Express what should happen to a completed step during retention.
    /// - Keep decision logic separate from retention application.
    ///
    /// IMPORTANT:
    /// - This is a decision value only.
    /// - It must not apply retention by itself.
    /// </summary>
    public enum AiExecutionRetentionAction
    {
        /// <summary>
        /// No retention action is required.
        /// </summary>
        None = 0,

        /// <summary>
        /// Keep the step unchanged in hot state.
        /// </summary>
        Keep = 1,

        /// <summary>
        /// Compact the step result payload while keeping the step in hot state.
        /// </summary>
        Compact = 2,

        /// <summary>
        /// Persist the full step externally and remove it from hot state.
        /// </summary>
        Evict = 3
    }
}