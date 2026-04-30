using Multiplexed.Abstractions.AI.Metrics.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Storage
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiStorageMetrics"/>.
    ///
    /// PURPOSE:
    /// - Track storage operations performed by the AI runtime.
    /// - Provide visibility into payload persistence and cache efficiency.
    ///
    /// THREAD SAFETY:
    /// - This implementation is safe for singleton usage.
    /// - Uses atomic operations and concurrent collections.
    ///
    /// IMPORTANT:
    /// - This class only records metrics.
    /// - It must not perform any storage, caching, or serialization logic.
    /// </summary>
    public sealed class AiStorageMetrics : IAiStorageMetrics
    {
        private long _payloadStoredCount;
        private long _payloadLoadedCount;
        private long _payloadStoreHitCount;
        private long _payloadStoreMissCount;
        private long _payloadStoreFailureCount;
        private long _totalPayloadStoredBytes;

        private readonly ConcurrentDictionary<string, long> _operationsByStorageKind = new();
        private readonly ConcurrentDictionary<string, long> _failuresByExceptionType = new();

        /// <inheritdoc />
        public void RecordPayloadStored(string executionId, string stepId, string storageKind, long? bytes)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _payloadStoredCount);
            IncrementStorageKind(storageKind);

            if (bytes.HasValue)
            {
                Interlocked.Add(ref _totalPayloadStoredBytes, Math.Max(0, bytes.Value));
            }
        }

        /// <inheritdoc />
        public void RecordPayloadLoaded(string executionId, string stepId, string storageKind)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _payloadLoadedCount);
            IncrementStorageKind(storageKind);
        }

        /// <inheritdoc />
        public void RecordPayloadStoreHit(string executionId, string stepId, string storageKind)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _payloadStoreHitCount);
            IncrementStorageKind(storageKind);
        }

        /// <inheritdoc />
        public void RecordPayloadStoreMiss(string executionId, string stepId, string storageKind)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _payloadStoreMissCount);
            IncrementStorageKind(storageKind);
        }

        /// <inheritdoc />
        public void RecordPayloadStoreFailure(string executionId, string stepId, string storageKind, Exception exception)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _payloadStoreFailureCount);
            IncrementStorageKind(storageKind);

            var key = exception?.GetType().Name ?? "unknown";

            _failuresByExceptionType.AddOrUpdate(
                key,
                _ => 1,
                (_, current) => current + 1);
        }

        /// <summary>
        /// Gets the number of payload store operations.
        /// </summary>
        public long PayloadStoredCount => Interlocked.Read(ref _payloadStoredCount);

        /// <summary>
        /// Gets the number of payload load operations.
        /// </summary>
        public long PayloadLoadedCount => Interlocked.Read(ref _payloadLoadedCount);

        /// <summary>
        /// Gets the number of cache hits.
        /// </summary>
        public long PayloadStoreHitCount => Interlocked.Read(ref _payloadStoreHitCount);

        /// <summary>
        /// Gets the number of cache misses.
        /// </summary>
        public long PayloadStoreMissCount => Interlocked.Read(ref _payloadStoreMissCount);

        /// <summary>
        /// Gets the number of storage failures.
        /// </summary>
        public long PayloadStoreFailureCount => Interlocked.Read(ref _payloadStoreFailureCount);

        /// <summary>
        /// Gets the total number of payload bytes stored.
        /// </summary>
        public long TotalPayloadStoredBytes => Interlocked.Read(ref _totalPayloadStoredBytes);

        /// <summary>
        /// Gets storage operations grouped by storage kind.
        /// </summary>
        public IReadOnlyDictionary<string, long> OperationsByStorageKind => _operationsByStorageKind;

        /// <summary>
        /// Gets failures grouped by exception type.
        /// </summary>
        public IReadOnlyDictionary<string, long> FailuresByExceptionType => _failuresByExceptionType;

        private void IncrementStorageKind(string storageKind)
        {
            var key = Normalize(storageKind);

            _operationsByStorageKind.AddOrUpdate(
                key,
                _ => 1,
                (_, current) => current + 1);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "unknown"
                : value.Trim();
        }
    }
}