namespace Multiplexed.Abstractions.AI.Execution.Payloads.Metrics
{
    /// <summary>
    /// Represents a point-in-time snapshot of AI payload metrics.
    ///
    /// PURPOSE:
    /// - Exposes current payload metrics in a read-only data structure.
    /// - Allows tests, diagnostics and observability integrations to inspect counters.
    ///
    /// DESIGN:
    /// - Immutable snapshot.
    /// - Does not mutate runtime counters.
    /// - Safe to expose outside the metrics implementation.
    /// </summary>
    public sealed class AiPayloadMetricsSnapshot
    {
        /// <summary>
        /// Gets the number of payloads kept inline in the execution state.
        /// </summary>
        public long InlineCount { get; init; }

        /// <summary>
        /// Gets the number of payloads externalized to payload storage.
        /// </summary>
        public long ExternalizedCount { get; init; }

        /// <summary>
        /// Gets the total number of bytes kept inline.
        /// </summary>
        public long InlineBytes { get; init; }

        /// <summary>
        /// Gets the total number of bytes externalized to payload storage.
        /// </summary>
        public long ExternalizedBytes { get; init; }

        /// <summary>
        /// Gets the number of payload resolution attempts.
        /// </summary>
        public long ResolveCount { get; init; }

        /// <summary>
        /// Gets the total number of bytes resolved from payload storage.
        /// </summary>
        public long ResolveBytes { get; init; }

        /// <summary>
        /// Gets the number of Redis payload cache hits.
        /// </summary>
        public long CacheHitCount { get; init; }

        /// <summary>
        /// Gets the number of Redis payload cache misses.
        /// </summary>
        public long CacheMissCount { get; init; }

        /// <summary>
        /// Gets the number of successful Redis payload cache writes.
        /// </summary>
        public long CacheWriteCount { get; init; }

        /// <summary>
        /// Gets the number of fallbacks from Redis cache to durable payload storage.
        /// </summary>
        public long CacheFallbackCount { get; init; }
    }
}