namespace Multiplexed.Abstractions.AI.Execution.Retention
{
    /// <summary>
    /// Defines retention options for AI execution state.
    ///
    /// PURPOSE:
    /// - Limits how many completed step states remain embedded in AiExecutionState.
    /// - Prevents long-running DAG executions from growing state indefinitely.
    /// - Keeps execution records lightweight after terminal completion.
    ///
    /// IMPORTANT:
    /// - Retention must not remove running, waiting, ready, or failed steps.
    /// - Retention should be applied conservatively after terminal completion first.
    /// </summary>
    public sealed class AiExecutionStateRetentionOptions
    {
        /// <summary>
        /// Gets or sets whether execution state retention is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of completed steps retained in state. defaults to 200.
        ///
        /// Retention should keep the most recent completed steps, but may remove older ones beyond this limit.
        /// </summary>
        public int MaxCompletedStepsInState { get; set; } = 200;
    }
}