using System;

namespace Multiplexed.Abstractions.AI.Observability.Metrics.Retention
{
    /// <summary>
    /// Records metrics for the retention execution phase.
    ///
    /// PURPOSE:
    /// - Track the actual work performed by retention.
    /// - Observe compaction, eviction, archival, and execution failures.
    ///
    /// EXECUTION PHASE:
    /// - Runs after a retention plan has been created.
    /// - Performs payload compaction, step eviction, or archive marking.
    /// </summary>
    public interface IAiRetentionExecutionMetrics
    {
        /// <summary>
        /// Records that a step payload was compacted or externalized.
        /// </summary>
        void RecordPayloadCompacted(string executionId, string stepId, long? beforeBytes, long? afterBytes);

        /// <summary>
        /// Records that a step was evicted from hot state.
        /// </summary>
        void RecordStepEvicted(string executionId, string stepId);

        /// <summary>
        /// Records that a step was marked as archived.
        /// </summary>
        void RecordStepMarkedArchived(string executionId, string stepId);

        /// <summary>
        /// Records that retention execution completed successfully.
        /// </summary>
        void RecordRetentionCompleted(string executionId);

        /// <summary>
        /// Records that retention execution failed.
        /// </summary>
        void RecordRetentionFailed(string executionId, Exception exception);
    }
}