namespace Multiplexed.AI.Runtime.Metrics.Retention
{
    /// <summary>
    /// Records metrics for the retention trigger phase.
    ///
    /// PURPOSE:
    /// - Track when retention is requested.
    /// - Track when retention is skipped before any decision or plan is created.
    /// - Preserve the reason for diagnostics and tuning.
    ///
    /// EXAMPLES:
    /// - Retention triggered because the state exceeded the maximum number of hot steps.
    /// - Retention skipped because the execution is not terminal.
    /// - Retention skipped because retention is disabled by configuration.
    /// </summary>
    public interface IAiRetentionTriggerMetrics
    {
        /// <summary>
        /// Records that retention was triggered for an execution.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="reason">The reason retention was triggered.</param>
        void RecordTriggered(string executionId, string reason);

        /// <summary>
        /// Records that retention was skipped before any retention decision was made.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="reason">The reason retention was skipped.</param>
        void RecordSkipped(string executionId, string reason);
    }
}