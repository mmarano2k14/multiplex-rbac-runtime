using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Retention
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRetentionPlanMetrics"/>.
    ///
    /// PURPOSE:
    /// - Track how retention plans are structured.
    /// - Provide insight into compaction and eviction workload.
    ///
    /// THREAD SAFETY:
    /// - Safe for singleton usage.
    /// - Uses atomic operations for counters.
    ///
    /// IMPORTANT:
    /// - This class does not execute retention.
    /// - It only observes and records plan characteristics.
    /// </summary>
    public sealed class AiRetentionPlanMetrics : IAiRetentionPlanMetrics
    {
        private long _planCreatedCount;
        private long _planEmptyCount;

        private long _totalCompactedSteps;
        private long _totalEvictedSteps;
        private long _totalKeepSteps;

        private long _lastCompactedSteps;
        private long _lastEvictedSteps;
        private long _lastKeepSteps;

        /// <inheritdoc />
        public void RecordPlanCreated(string executionId, int compactSteps, int evictSteps, int keepSteps)
        {
            _ = executionId;

            Interlocked.Increment(ref _planCreatedCount);

            Interlocked.Add(ref _totalCompactedSteps, compactSteps);
            Interlocked.Add(ref _totalEvictedSteps, evictSteps);
            Interlocked.Add(ref _totalKeepSteps, keepSteps);

            Interlocked.Exchange(ref _lastCompactedSteps, compactSteps);
            Interlocked.Exchange(ref _lastEvictedSteps, evictSteps);
            Interlocked.Exchange(ref _lastKeepSteps, keepSteps);
        }

        /// <inheritdoc />
        public void RecordPlanEmpty(string executionId)
        {
            _ = executionId;

            Interlocked.Increment(ref _planEmptyCount);
        }

        /// <summary>
        /// Gets the number of created retention plans.
        /// </summary>
        public long PlanCreatedCount => Interlocked.Read(ref _planCreatedCount);

        /// <summary>
        /// Gets the number of times no plan was created.
        /// </summary>
        public long PlanEmptyCount => Interlocked.Read(ref _planEmptyCount);

        /// <summary>
        /// Gets total number of steps scheduled for compaction.
        /// </summary>
        public long TotalCompactedSteps => Interlocked.Read(ref _totalCompactedSteps);

        /// <summary>
        /// Gets total number of steps scheduled for eviction.
        /// </summary>
        public long TotalEvictedSteps => Interlocked.Read(ref _totalEvictedSteps);

        /// <summary>
        /// Gets total number of steps kept in hot state.
        /// </summary>
        public long TotalKeepSteps => Interlocked.Read(ref _totalKeepSteps);

        /// <summary>
        /// Gets last recorded compact step count.
        /// </summary>
        public long LastCompactedSteps => Interlocked.Read(ref _lastCompactedSteps);

        /// <summary>
        /// Gets last recorded evict step count.
        /// </summary>
        public long LastEvictedSteps => Interlocked.Read(ref _lastEvictedSteps);

        /// <summary>
        /// Gets last recorded kept step count.
        /// </summary>
        public long LastKeepSteps => Interlocked.Read(ref _lastKeepSteps);
    }
}