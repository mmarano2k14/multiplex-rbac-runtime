using System;

namespace Multiplexed.Abstractions.AI.Metrics.Storage
{
    /// <summary>
    /// Records metrics for AI runtime storage operations.
    ///
    /// PURPOSE:
    /// - Provide observability over payload storage lifecycle.
    /// - Track persistence operations across different storage backends.
    /// - Measure cache efficiency (hit/miss).
    /// - Track failures for diagnostics and reliability analysis.
    ///
    /// STORAGE LAYER:
    /// - Can include Redis (cache), MongoDB (persistence), or other providers.
    /// - Identified via <paramref name="storageKind"/>.
    ///
    /// IMPORTANT:
    /// - This interface is observational only.
    /// - It must not perform any storage or caching logic.
    /// </summary>
    public interface IAiStorageMetrics
    {
        /// <summary>
        /// Records that a payload was stored.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="storageKind">The storage backend (e.g. redis, mongo).</param>
        /// <param name="bytes">The size of the payload in bytes, if known.</param>
        void RecordPayloadStored(string executionId, string stepId, string storageKind, long? bytes);

        /// <summary>
        /// Records that a payload was loaded from storage.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="storageKind">The storage backend.</param>
        void RecordPayloadLoaded(string executionId, string stepId, string storageKind);

        /// <summary>
        /// Records that a cache hit occurred when retrieving a payload.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="storageKind">The storage backend.</param>
        void RecordPayloadStoreHit(string executionId, string stepId, string storageKind);

        /// <summary>
        /// Records that a cache miss occurred when retrieving a payload.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="storageKind">The storage backend.</param>
        void RecordPayloadStoreMiss(string executionId, string stepId, string storageKind);

        /// <summary>
        /// Records that a storage operation failed.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="storageKind">The storage backend.</param>
        /// <param name="exception">The exception that occurred.</param>
        void RecordPayloadStoreFailure(string executionId, string stepId, string storageKind, Exception exception);
    }
}