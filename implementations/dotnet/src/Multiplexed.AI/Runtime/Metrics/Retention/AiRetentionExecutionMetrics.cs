using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Retention
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRetentionExecutionMetrics"/>.
    ///
    /// PURPOSE:
    /// - Track the actual retention work performed by the runtime.
    /// - Provide diagnostics for payload compaction, eviction, archival, and failures.
    ///
    /// THREAD SAFETY:
    /// - Safe for singleton usage.
    /// - Uses atomic operations and concurrent dictionaries.
    ///
    /// IMPORTANT:
    /// - This class observes retention execution only.
    /// - It must not perform compaction, eviction, archival, or persistence.
    /// </summary>
    public sealed class AiRetentionExecutionMetrics : IAiRetentionExecutionMetrics
    {
        private long _payloadCompactedCount;
        private long _stepEvictedCount;
        private long _stepMarkedArchivedCount;
        private long _retentionCompletedCount;
        private long _retentionFailedCount;

        private long _totalBeforeBytes;
        private long _totalAfterBytes;
        private long _totalBytesSaved;

        private readonly ConcurrentDictionary<string, long> _failuresByExceptionType = new();

        /// <inheritdoc />
        public void RecordPayloadCompacted(string executionId, string stepId, long? beforeBytes, long? afterBytes)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _payloadCompactedCount);

            if (beforeBytes.HasValue)
            {
                Interlocked.Add(ref _totalBeforeBytes, Math.Max(0, beforeBytes.Value));
            }

            if (afterBytes.HasValue)
            {
                Interlocked.Add(ref _totalAfterBytes, Math.Max(0, afterBytes.Value));
            }

            if (beforeBytes.HasValue && afterBytes.HasValue)
            {
                var saved = Math.Max(0, beforeBytes.Value - afterBytes.Value);
                Interlocked.Add(ref _totalBytesSaved, saved);
            }
        }

        /// <inheritdoc />
        public void RecordStepEvicted(string executionId, string stepId)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _stepEvictedCount);
        }

        /// <inheritdoc />
        public void RecordStepMarkedArchived(string executionId, string stepId)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _stepMarkedArchivedCount);
        }

        /// <inheritdoc />
        public void RecordRetentionCompleted(string executionId)
        {
            _ = executionId;

            Interlocked.Increment(ref _retentionCompletedCount);
        }

        /// <inheritdoc />
        public void RecordRetentionFailed(string executionId, Exception exception)
        {
            _ = executionId;

            Interlocked.Increment(ref _retentionFailedCount);

            var key = exception?.GetType().Name ?? "unknown";
            _failuresByExceptionType.AddOrUpdate(
                key,
                _ => 1,
                (_, current) => current + 1);
        }

        /// <summary>
        /// Gets the number of compacted payloads.
        /// </summary>
        public long PayloadCompactedCount => Interlocked.Read(ref _payloadCompactedCount);

        /// <summary>
        /// Gets the number of evicted steps.
        /// </summary>
        public long StepEvictedCount => Interlocked.Read(ref _stepEvictedCount);

        /// <summary>
        /// Gets the number of steps marked as archived.
        /// </summary>
        public long StepMarkedArchivedCount => Interlocked.Read(ref _stepMarkedArchivedCount);

        /// <summary>
        /// Gets the number of completed retention executions.
        /// </summary>
        public long RetentionCompletedCount => Interlocked.Read(ref _retentionCompletedCount);

        /// <summary>
        /// Gets the number of failed retention executions.
        /// </summary>
        public long RetentionFailedCount => Interlocked.Read(ref _retentionFailedCount);

        /// <summary>
        /// Gets the total observed payload size before compaction.
        /// </summary>
        public long TotalBeforeBytes => Interlocked.Read(ref _totalBeforeBytes);

        /// <summary>
        /// Gets the total observed payload size after compaction.
        /// </summary>
        public long TotalAfterBytes => Interlocked.Read(ref _totalAfterBytes);

        /// <summary>
        /// Gets the total observed bytes saved by compaction.
        /// </summary>
        public long TotalBytesSaved => Interlocked.Read(ref _totalBytesSaved);

        /// <summary>
        /// Gets failures grouped by exception type.
        /// </summary>
        public IReadOnlyDictionary<string, long> FailuresByExceptionType => _failuresByExceptionType;
    }
}