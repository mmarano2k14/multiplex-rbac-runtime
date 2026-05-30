namespace Multiplexed.Abstractions.AI.Observability.Metrics.HotState
{
    /// <summary>
    /// Records metrics for the hot execution state.
    ///
    /// PURPOSE:
    /// - Observe the size and lifecycle of the in-memory execution state.
    /// - Track when steps are added, removed, compacted, or measured.
    /// - Help validate that retention keeps the hot state bounded.
    ///
    /// HOT STATE:
    /// - Represents the active execution state kept available for runtime decisions.
    /// - Should remain bounded when retention and compaction are enabled.
    ///
    /// IMPORTANT:
    /// - This interface is observational only.
    /// - It must not modify execution state.
    /// </summary>
    public interface IAiHotStateMetrics
    {
        /// <summary>
        /// Records that a step was added to the hot execution state.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        void RecordStateStepAdded(string executionId, string stepId);

        /// <summary>
        /// Records that a step was removed from the hot execution state.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        void RecordStateStepRemoved(string executionId, string stepId);

        /// <summary>
        /// Records that the hot execution state was compacted.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="beforeSteps">The number of steps before compaction.</param>
        /// <param name="afterSteps">The number of steps after compaction.</param>
        void RecordStateCompacted(string executionId, int beforeSteps, int afterSteps);

        /// <summary>
        /// Records an observed hot state size.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepCount">The number of steps currently in hot state.</param>
        /// <param name="estimatedBytes">The estimated state size in bytes, if known.</param>
        void RecordStateSizeObserved(string executionId, int stepCount, long? estimatedBytes);
    }
}