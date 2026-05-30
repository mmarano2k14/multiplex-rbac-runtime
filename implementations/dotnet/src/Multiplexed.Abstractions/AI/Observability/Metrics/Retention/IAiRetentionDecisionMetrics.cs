namespace Multiplexed.Abstractions.AI.Observability.Metrics.Retention
{
    /// <summary>
    /// Records metrics for the retention decision phase.
    ///
    /// PURPOSE:
    /// - Track what the retention policy decided before a plan is created.
    /// - Measure whether compaction, eviction, both, or no action is required.
    /// - Help tune retention thresholds over time.
    ///
    /// DECISION PHASE:
    /// - Runs after retention has been triggered.
    /// - Evaluates the current execution state footprint.
    /// - Determines whether retention work is necessary.
    /// </summary>
    public interface IAiRetentionDecisionMetrics
    {
        /// <summary>
        /// Records that payload compaction is required.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepCount">The current number of steps in hot state.</param>
        /// <param name="threshold">The configured compaction threshold.</param>
        void RecordCompactionRequired(string executionId, int stepCount, int threshold);

        /// <summary>
        /// Records that step eviction is required.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepCount">The current number of steps in hot state.</param>
        /// <param name="maxSteps">The maximum number of steps allowed in hot state.</param>
        void RecordEvictionRequired(string executionId, int stepCount, int maxSteps);

        /// <summary>
        /// Records that no retention action is required.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepCount">The current number of steps in hot state.</param>
        void RecordNoActionRequired(string executionId, int stepCount);
    }
}