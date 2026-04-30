using Multiplexed.Abstractions.AI.Metrics.Retention;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Retention
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRetentionDecisionMetrics"/>.
    ///
    /// PURPOSE:
    /// - Track retention policy decisions.
    /// - Provide lightweight counters for diagnostics and tests.
    ///
    /// THREAD SAFETY:
    /// - This implementation is safe to use as a singleton.
    /// - Counters are updated using atomic operations.
    ///
    /// IMPORTANT:
    /// - This class records decisions only.
    /// - It does not create retention plans.
    /// - It does not perform compaction, eviction, or archival.
    /// </summary>
    public sealed class AiRetentionDecisionMetrics : IAiRetentionDecisionMetrics
    {
        private long _compactionRequiredCount;
        private long _evictionRequiredCount;
        private long _noActionRequiredCount;

        private long _lastObservedStepCount;
        private long _lastCompactionThreshold;
        private long _lastEvictionMaxSteps;

        /// <inheritdoc />
        public void RecordCompactionRequired(string executionId, int stepCount, int threshold)
        {
            _ = executionId;

            Interlocked.Increment(ref _compactionRequiredCount);
            Interlocked.Exchange(ref _lastObservedStepCount, stepCount);
            Interlocked.Exchange(ref _lastCompactionThreshold, threshold);
        }

        /// <inheritdoc />
        public void RecordEvictionRequired(string executionId, int stepCount, int maxSteps)
        {
            _ = executionId;

            Interlocked.Increment(ref _evictionRequiredCount);
            Interlocked.Exchange(ref _lastObservedStepCount, stepCount);
            Interlocked.Exchange(ref _lastEvictionMaxSteps, maxSteps);
        }

        /// <inheritdoc />
        public void RecordNoActionRequired(string executionId, int stepCount)
        {
            _ = executionId;

            Interlocked.Increment(ref _noActionRequiredCount);
            Interlocked.Exchange(ref _lastObservedStepCount, stepCount);
        }

        /// <summary>
        /// Gets the number of decisions where compaction was required.
        /// </summary>
        public long CompactionRequiredCount => Interlocked.Read(ref _compactionRequiredCount);

        /// <summary>
        /// Gets the number of decisions where eviction was required.
        /// </summary>
        public long EvictionRequiredCount => Interlocked.Read(ref _evictionRequiredCount);

        /// <summary>
        /// Gets the number of decisions where no retention action was required.
        /// </summary>
        public long NoActionRequiredCount => Interlocked.Read(ref _noActionRequiredCount);

        /// <summary>
        /// Gets the last observed number of steps in hot state.
        /// </summary>
        public long LastObservedStepCount => Interlocked.Read(ref _lastObservedStepCount);

        /// <summary>
        /// Gets the last observed compaction threshold.
        /// </summary>
        public long LastCompactionThreshold => Interlocked.Read(ref _lastCompactionThreshold);

        /// <summary>
        /// Gets the last observed maximum number of hot steps.
        /// </summary>
        public long LastEvictionMaxSteps => Interlocked.Read(ref _lastEvictionMaxSteps);
    }
}