namespace Multiplexed.Abstractions.AI.Execution.Payloads.Metrics
{
    /// <summary>
    /// Defines runtime metrics for AI payload compaction, storage and resolution.
    ///
    /// PURPOSE:
    /// - Tracks whether payloads remain inline in the execution state or are externalized.
    /// - Tracks the amount of data kept inline versus moved to external payload storage.
    /// - Provides future-ready counters for Redis payload cache integration.
    ///
    /// DESIGN:
    /// - Metrics are observational only and must never affect runtime behavior.
    /// - Implementations must be thread-safe because executions can run concurrently.
    /// - Implementations should be lightweight and non-blocking.
    ///
    /// IMPORTANT:
    /// - Inline/externalized metrics should be recorded by the compactor, because the
    ///   compactor owns the decision of keeping data inline or replacing it with a
    ///   payload reference.
    /// - Cache metrics are reserved for the Redis cached payload store.
    /// </summary>
    public interface IAiPayloadMetrics
    {
        /// <summary>
        /// Records a payload that remained inline in the execution state.
        /// </summary>
        /// <param name="bytes">The payload size in bytes.</param>
        void RecordInlinePayload(long bytes);

        /// <summary>
        /// Records a payload that was externalized to the payload store.
        /// </summary>
        /// <param name="bytes">The payload size in bytes.</param>
        void RecordExternalizedPayload(long bytes);

        /// <summary>
        /// Records a payload resolution attempt.
        /// </summary>
        void RecordPayloadResolve();

        /// <summary>
        /// Records the number of bytes resolved from payload storage.
        /// </summary>
        /// <param name="bytes">The resolved payload size in bytes.</param>
        void RecordPayloadResolveBytes(long bytes);

        /// <summary>
        /// Records a Redis payload cache hit.
        ///
        /// FUTURE USAGE:
        /// - Intended for RedisCachedAiPayloadStore.
        /// </summary>
        void RecordCacheHit();

        /// <summary>
        /// Records a Redis payload cache miss.
        ///
        /// FUTURE USAGE:
        /// - Intended for RedisCachedAiPayloadStore.
        /// </summary>
        void RecordCacheMiss();

        /// <summary>
        /// Records a successful Redis payload cache write.
        ///
        /// FUTURE USAGE:
        /// - Intended for RedisCachedAiPayloadStore.
        /// </summary>
        void RecordCacheWrite();

        /// <summary>
        /// Records a fallback from Redis cache to durable payload storage.
        ///
        /// FUTURE USAGE:
        /// - Intended for RedisCachedAiPayloadStore when Redis misses or is unavailable.
        /// </summary>
        void RecordCacheFallback();
    }
}