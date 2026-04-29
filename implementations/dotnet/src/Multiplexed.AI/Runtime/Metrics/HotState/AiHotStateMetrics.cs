using System;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.HotState
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiHotStateMetrics"/>.
    ///
    /// PURPOSE:
    /// - Track hot state growth and reduction.
    /// - Provide visibility into state compaction effectiveness.
    ///
    /// THREAD SAFETY:
    /// - This implementation is safe for singleton usage.
    /// - Uses atomic operations for all counters.
    ///
    /// IMPORTANT:
    /// - This class only records metrics.
    /// - It must not change execution state or retention behavior.
    /// </summary>
    public sealed class AiHotStateMetrics : IAiHotStateMetrics
    {
        private long _stateStepAddedCount;
        private long _stateStepRemovedCount;
        private long _stateCompactedCount;
        private long _stateSizeObservedCount;

        private long _lastObservedStepCount;
        private long _lastEstimatedBytes;
        private long _lastCompactionBeforeSteps;
        private long _lastCompactionAfterSteps;
        private long _totalStepsRemovedByCompaction;

        /// <inheritdoc />
        public void RecordStateStepAdded(string executionId, string stepId)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _stateStepAddedCount);
        }

        /// <inheritdoc />
        public void RecordStateStepRemoved(string executionId, string stepId)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _stateStepRemovedCount);
        }

        /// <inheritdoc />
        public void RecordStateCompacted(string executionId, int beforeSteps, int afterSteps)
        {
            _ = executionId;

            var safeBeforeSteps = Math.Max(0, beforeSteps);
            var safeAfterSteps = Math.Max(0, afterSteps);
            var removed = Math.Max(0, safeBeforeSteps - safeAfterSteps);

            Interlocked.Increment(ref _stateCompactedCount);
            Interlocked.Exchange(ref _lastCompactionBeforeSteps, safeBeforeSteps);
            Interlocked.Exchange(ref _lastCompactionAfterSteps, safeAfterSteps);
            Interlocked.Add(ref _totalStepsRemovedByCompaction, removed);
        }

        /// <inheritdoc />
        public void RecordStateSizeObserved(string executionId, int stepCount, long? estimatedBytes)
        {
            _ = executionId;

            Interlocked.Increment(ref _stateSizeObservedCount);
            Interlocked.Exchange(ref _lastObservedStepCount, Math.Max(0, stepCount));

            if (estimatedBytes.HasValue)
            {
                Interlocked.Exchange(ref _lastEstimatedBytes, Math.Max(0, estimatedBytes.Value));
            }
        }

        /// <summary>
        /// Gets the number of steps added to hot state.
        /// </summary>
        public long StateStepAddedCount => Interlocked.Read(ref _stateStepAddedCount);

        /// <summary>
        /// Gets the number of steps removed from hot state.
        /// </summary>
        public long StateStepRemovedCount => Interlocked.Read(ref _stateStepRemovedCount);

        /// <summary>
        /// Gets the number of hot state compaction events.
        /// </summary>
        public long StateCompactedCount => Interlocked.Read(ref _stateCompactedCount);

        /// <summary>
        /// Gets the number of state size observations.
        /// </summary>
        public long StateSizeObservedCount => Interlocked.Read(ref _stateSizeObservedCount);

        /// <summary>
        /// Gets the last observed number of steps in hot state.
        /// </summary>
        public long LastObservedStepCount => Interlocked.Read(ref _lastObservedStepCount);

        /// <summary>
        /// Gets the last observed estimated state size in bytes.
        /// </summary>
        public long LastEstimatedBytes => Interlocked.Read(ref _lastEstimatedBytes);

        /// <summary>
        /// Gets the last observed step count before compaction.
        /// </summary>
        public long LastCompactionBeforeSteps => Interlocked.Read(ref _lastCompactionBeforeSteps);

        /// <summary>
        /// Gets the last observed step count after compaction.
        /// </summary>
        public long LastCompactionAfterSteps => Interlocked.Read(ref _lastCompactionAfterSteps);

        /// <summary>
        /// Gets the total number of steps removed by hot state compaction.
        /// </summary>
        public long TotalStepsRemovedByCompaction => Interlocked.Read(ref _totalStepsRemovedByCompaction);
    }
}