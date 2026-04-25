namespace Multiplexed.Abstractions.AI.Execution.Retention
{
    /// <summary>
    /// Applies retention rules to an execution state.
    ///
    /// PURPOSE:
    /// - Limits the number of completed steps retained in memory.
    /// - Prevents unbounded growth of AiExecutionState.
    ///
    /// IMPORTANT:
    /// - Must not remove non-terminal steps.
    /// - Must preserve determinism and replay safety.
    /// </summary>
    public interface IAiExecutionStateRetentionPolicy
    {
        /// <summary>
        /// Applies retention rules to the provided execution state.
        /// </summary>
        void Apply(AiExecutionState state);
    }
}