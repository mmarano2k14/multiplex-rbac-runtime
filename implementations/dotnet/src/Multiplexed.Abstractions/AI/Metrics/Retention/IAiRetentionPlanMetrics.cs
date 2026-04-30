namespace Multiplexed.Abstractions.AI.Metrics.Retention
{
    /// <summary>
    /// Records metrics for the retention planning phase.
    ///
    /// PURPOSE:
    /// - Track how retention work is structured before execution.
    /// - Measure how many steps will be compacted, evicted, or kept.
    ///
    /// PLAN PHASE:
    /// - Occurs after decision phase.
    /// - Translates decisions into actionable operations.
    ///
    /// EXAMPLES:
    /// - Compact 10 steps, evict 5, keep 20.
    /// - No plan created because no action required.
    /// </summary>
    public interface IAiRetentionPlanMetrics
    {
        /// <summary>
        /// Records that a retention plan was created.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="compactSteps">Number of steps to compact.</param>
        /// <param name="evictSteps">Number of steps to evict.</param>
        /// <param name="keepSteps">Number of steps kept in hot state.</param>
        void RecordPlanCreated(string executionId, int compactSteps, int evictSteps, int keepSteps);

        /// <summary>
        /// Records that no retention plan was created.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        void RecordPlanEmpty(string executionId);
    }
}