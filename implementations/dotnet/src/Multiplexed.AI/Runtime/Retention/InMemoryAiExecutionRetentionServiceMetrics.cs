using Multiplexed.Abstractions.AI.Execution.Retention;

namespace Multiplexed.AI.Runtime.Execution.Retention
{
    /// <summary>
    /// In-memory metrics implementation for the new retention service.
    ///
    /// PURPOSE:
    /// - Useful for tests.
    /// - Useful for local diagnostics.
    ///
    /// IMPORTANT:
    /// - Not intended as a durable metrics backend.
    /// - Production can replace this with Prometheus/OpenTelemetry later.
    /// </summary>
    public sealed class InMemoryAiExecutionRetentionServiceMetrics
        : IAiExecutionRetentionServiceMetrics
    {
        private readonly object _gate = new();

        private AiExecutionRetentionMode _lastMode;
        private int _totalStepsBefore;
        private int _totalStepsAfter;
        private int _stepsPlannedForCompaction;
        private int _stepsPlannedForEviction;
        private int _compactedSteps;
        private int _evictedSteps;

        public void RecordEvaluation(
            AiExecutionRetentionMode mode,
            int totalStepsBefore,
            int stepsToCompact,
            int stepsToEvict)
        {
            lock (_gate)
            {
                _lastMode = mode;
                _totalStepsBefore = totalStepsBefore;
                _stepsPlannedForCompaction = stepsToCompact;
                _stepsPlannedForEviction = stepsToEvict;
            }
        }

        public void RecordCompacted(string stepName)
        {
            lock (_gate)
            {
                _compactedSteps++;
            }
        }

        public void RecordEvicted(string stepName)
        {
            lock (_gate)
            {
                _evictedSteps++;
            }
        }

        public void RecordCompleted(
            AiExecutionRetentionMode mode,
            int totalStepsBefore,
            int totalStepsAfter)
        {
            lock (_gate)
            {
                _lastMode = mode;
                _totalStepsBefore = totalStepsBefore;
                _totalStepsAfter = totalStepsAfter;
            }
        }

        public AiExecutionRetentionServiceMetricsSnapshot Snapshot()
        {
            lock (_gate)
            {
                return new AiExecutionRetentionServiceMetricsSnapshot
                {
                    LastMode = _lastMode,
                    TotalStepsBefore = _totalStepsBefore,
                    TotalStepsAfter = _totalStepsAfter,
                    StepsPlannedForCompaction = _stepsPlannedForCompaction,
                    StepsPlannedForEviction = _stepsPlannedForEviction,
                    CompactedSteps = _compactedSteps,
                    EvictedSteps = _evictedSteps
                };
            }
        }
    }
}