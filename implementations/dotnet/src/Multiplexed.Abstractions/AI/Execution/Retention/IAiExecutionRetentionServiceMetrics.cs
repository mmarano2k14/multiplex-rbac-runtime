namespace Multiplexed.Abstractions.AI.Execution.Retention
{
    /// <summary>
    /// Records metrics emitted by the new execution retention service.
    ///
    /// PURPOSE:
    /// - Track Compact / Evict / Hybrid activity.
    /// - Validate memory reduction in tests.
    /// - Provide observability for production runtime retention.
    ///
    /// IMPORTANT:
    /// - This belongs to the new retention system.
    /// - It replaces legacy IAiExecutionRetentionMetrics usage for new tests.
    /// </summary>
    public interface IAiExecutionRetentionServiceMetrics
    {
        void RecordEvaluation(
            AiExecutionRetentionMode mode,
            int totalStepsBefore,
            int stepsToCompact,
            int stepsToEvict);

        void RecordCompacted(string stepName);

        void RecordEvicted(string stepName);

        void RecordCompleted(
            AiExecutionRetentionMode mode,
            int totalStepsBefore,
            int totalStepsAfter);
    }
}