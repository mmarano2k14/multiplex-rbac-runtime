using Multiplexed.Abstractions.AI.Execution.Metrics;

namespace Multiplexed.AI.Runtime.Observability.Metrics
{
    /// <summary>
    /// In-memory implementation of execution retention metrics.
    ///
    /// PURPOSE:
    /// - Stores the latest retention metrics snapshot.
    /// - Enables test validation and runtime observability.
    /// </summary>
    public sealed class InMemoryAiExecutionRetentionMetrics
        : IAiExecutionRetentionMetrics
    {
        private int _totalBefore;
        private int _totalAfter;
        private int _evicted;
        private int _active;
        private int _pending;

        public void RecordRetention(
            int totalStepsBefore,
            int totalStepsAfter,
            int completedEvicted,
            int activeSteps,
            int pendingSteps)
        {
            _totalBefore = totalStepsBefore;
            _totalAfter = totalStepsAfter;
            _evicted += completedEvicted;
            _active = activeSteps;
            _pending = pendingSteps;
        }

        public AiExecutionRetentionMetricsSnapshot Snapshot()
        {
            return new AiExecutionRetentionMetricsSnapshot
            {
                TotalStepsBefore = _totalBefore,
                TotalStepsAfter = _totalAfter,
                EvictedSteps = _evicted,
                ActiveSteps = _active,
                PendingSteps = _pending
            };
        }
    }
}