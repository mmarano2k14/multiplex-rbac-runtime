using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Metrics
{
    /// <summary>
    /// Thread-safe in-memory implementation of <see cref="IAiPayloadMetrics"/>.
    ///
    /// PURPOSE:
    /// - Provides lightweight runtime metrics for payload compaction and storage behavior.
    /// - Supports tests and local diagnostics without requiring an external metrics backend.
    /// - Prepares the runtime for Redis payload cache observability.
    ///
    /// DESIGN:
    /// - Uses atomic counters through <see cref="Interlocked"/>.
    /// - Does not allocate during normal counter increments.
    /// - Does not throw for negative byte values; values are normalized to zero.
    ///
    /// IMPORTANT:
    /// - This implementation is process-local.
    /// - It is not intended as a distributed metrics backend.
    /// - Production exporters can later wrap or replace this implementation.
    /// </summary>
    public sealed class InMemoryAiPayloadMetrics : IAiPayloadMetrics
    {
        private long _inlineCount;
        private long _externalizedCount;
        private long _inlineBytes;
        private long _externalizedBytes;

        private long _resolveCount;
        private long _resolveBytes;

        private long _cacheHitCount;
        private long _cacheMissCount;
        private long _cacheWriteCount;
        private long _cacheFallbackCount;

        /// <inheritdoc />
        public void RecordInlinePayload(long bytes)
        {
            Interlocked.Increment(ref _inlineCount);
            Interlocked.Add(ref _inlineBytes, NormalizeBytes(bytes));
        }

        /// <inheritdoc />
        public void RecordExternalizedPayload(long bytes)
        {
            Interlocked.Increment(ref _externalizedCount);
            Interlocked.Add(ref _externalizedBytes, NormalizeBytes(bytes));
        }

        /// <inheritdoc />
        public void RecordPayloadResolve()
        {
            Interlocked.Increment(ref _resolveCount);
        }

        /// <inheritdoc />
        public void RecordPayloadResolveBytes(long bytes)
        {
            Interlocked.Add(ref _resolveBytes, NormalizeBytes(bytes));
        }

        /// <inheritdoc />
        public void RecordCacheHit()
        {
            Interlocked.Increment(ref _cacheHitCount);
        }

        /// <inheritdoc />
        public void RecordCacheMiss()
        {
            Interlocked.Increment(ref _cacheMissCount);
        }

        /// <inheritdoc />
        public void RecordCacheWrite()
        {
            Interlocked.Increment(ref _cacheWriteCount);
        }

        /// <inheritdoc />
        public void RecordCacheFallback()
        {
            Interlocked.Increment(ref _cacheFallbackCount);
        }

        /// <summary>
        /// Creates a point-in-time snapshot of the current payload metrics.
        /// </summary>
        /// <returns>The current metrics snapshot.</returns>
        public AiPayloadMetricsSnapshot Snapshot()
        {
            return new AiPayloadMetricsSnapshot
            {
                InlineCount = Interlocked.Read(ref _inlineCount),
                ExternalizedCount = Interlocked.Read(ref _externalizedCount),
                InlineBytes = Interlocked.Read(ref _inlineBytes),
                ExternalizedBytes = Interlocked.Read(ref _externalizedBytes),
                ResolveCount = Interlocked.Read(ref _resolveCount),
                ResolveBytes = Interlocked.Read(ref _resolveBytes),
                CacheHitCount = Interlocked.Read(ref _cacheHitCount),
                CacheMissCount = Interlocked.Read(ref _cacheMissCount),
                CacheWriteCount = Interlocked.Read(ref _cacheWriteCount),
                CacheFallbackCount = Interlocked.Read(ref _cacheFallbackCount)
            };
        }

        /// <summary>
        /// Normalizes byte values before adding them to counters.
        /// </summary>
        /// <param name="bytes">The input byte value.</param>
        /// <returns>A non-negative byte value.</returns>
        private static long NormalizeBytes(long bytes)
        {
            return bytes < 0 ? 0 : bytes;
        }
    }
}