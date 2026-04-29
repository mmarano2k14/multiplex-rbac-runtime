using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Retention
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRetentionTriggerMetrics"/>.
    ///
    /// PURPOSE:
    /// - Track retention trigger activity inside the runtime.
    /// - Keep lightweight counters for diagnostics and integration tests.
    ///
    /// THREAD SAFETY:
    /// - This implementation is safe to use as a singleton.
    /// - Counters are updated using atomic operations.
    /// - Reason-based counts are stored in concurrent dictionaries.
    ///
    /// IMPORTANT:
    /// - This implementation is intentionally in-memory.
    /// - It does not export to Prometheus, OpenTelemetry, MongoDB, or logs directly.
    /// - Exporters can be added later without changing the runtime contract.
    /// </summary>
    public sealed class AiRetentionTriggerMetrics : IAiRetentionTriggerMetrics
    {
        private long _triggeredCount;
        private long _skippedCount;

        private readonly ConcurrentDictionary<string, long> _triggeredByReason = new();
        private readonly ConcurrentDictionary<string, long> _skippedByReason = new();

        /// <inheritdoc />
        public void RecordTriggered(string executionId, string reason)
        {
            _ = executionId;

            Interlocked.Increment(ref _triggeredCount);
            IncrementReason(_triggeredByReason, reason);
        }

        /// <inheritdoc />
        public void RecordSkipped(string executionId, string reason)
        {
            _ = executionId;

            Interlocked.Increment(ref _skippedCount);
            IncrementReason(_skippedByReason, reason);
        }

        /// <summary>
        /// Gets the total number of retention trigger events.
        /// </summary>
        public long TriggeredCount => Interlocked.Read(ref _triggeredCount);

        /// <summary>
        /// Gets the total number of retention skipped events.
        /// </summary>
        public long SkippedCount => Interlocked.Read(ref _skippedCount);

        /// <summary>
        /// Gets the number of retention trigger events grouped by reason.
        /// </summary>
        public IReadOnlyDictionary<string, long> TriggeredByReason => _triggeredByReason;

        /// <summary>
        /// Gets the number of retention skipped events grouped by reason.
        /// </summary>
        public IReadOnlyDictionary<string, long> SkippedByReason => _skippedByReason;

        private static void IncrementReason(
            ConcurrentDictionary<string, long> target,
            string reason)
        {
            var key = NormalizeReason(reason);

            target.AddOrUpdate(
                key,
                _ => 1,
                (_, current) => current + 1);
        }

        private static string NormalizeReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason)
                ? "unknown"
                : reason.Trim();
        }
    }
}