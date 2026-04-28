using Multiplexed.Abstractions.AI.Execution.Retention.Models;

namespace Multiplexed.Abstractions.AI.Execution.Retention.Services
{
    /// <summary>
    /// Snapshot of retention service metrics.
    /// Used mainly by tests and diagnostics.
    /// </summary>
    public sealed class AiExecutionRetentionServiceMetricsSnapshot
    {
        public AiExecutionRetentionMode LastMode { get; init; }

        public int TotalStepsBefore { get; init; }

        public int TotalStepsAfter { get; init; }

        public int StepsPlannedForCompaction { get; init; }

        public int StepsPlannedForEviction { get; init; }

        public int CompactedSteps { get; init; }

        public int EvictedSteps { get; init; }
    }
}