namespace Multiplexed.Abstractions.AI.Observability.Metrics.Retention
{
    /// <summary>
    /// Aggregates all metrics related to the retention lifecycle.
    ///
    /// PURPOSE:
    /// - Provide structured observability over retention operations.
    /// - Separate each phase of retention for better diagnostics and analysis.
    ///
    /// RETENTION PIPELINE:
    /// 1. Trigger   → Why retention was invoked
    /// 2. Decision  → What needs to be done
    /// 3. Plan      → How retention will be executed
    /// 4. Execution → Actual compaction / eviction / archival
    ///
    /// DESIGN:
    /// - Each stage exposes a dedicated metrics object.
    /// - Allows fine-grained tracking without mixing responsibilities.
    /// </summary>
    public interface IAiRetentionMetrics
    {
        /// <summary>
        /// Gets metrics related to retention trigger phase.
        /// </summary>
        IAiRetentionTriggerMetrics Trigger { get; }

        /// <summary>
        /// Gets metrics related to retention decision phase.
        /// </summary>
        IAiRetentionDecisionMetrics Decision { get; }

        /// <summary>
        /// Gets metrics related to retention planning phase.
        /// </summary>
        IAiRetentionPlanMetrics Plan { get; }

        /// <summary>
        /// Gets metrics related to retention execution phase.
        /// </summary>
        IAiRetentionExecutionMetrics Execution { get; }
    }
}