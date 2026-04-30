using Multiplexed.Abstractions.AI.Metrics.Resolvers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Resolvers
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiResolverMetrics"/>.
    ///
    /// PURPOSE:
    /// - Track resolver behavior during runtime execution.
    /// - Provide diagnostics for input binding, path resolution, and missing values.
    ///
    /// THREAD SAFETY:
    /// - This implementation is safe for singleton usage.
    /// - Uses atomic operations and concurrent collections.
    ///
    /// IMPORTANT:
    /// - This class only records resolver metrics.
    /// - It must not perform resolution or influence resolver results.
    /// </summary>
    public sealed class AiResolverMetrics : IAiResolverMetrics
    {
        private long _resolvedStartedCount;
        private long _resolvedSuccessCount;
        private long _resolvedMissCount;
        private long _resolvedFailedCount;

        private readonly ConcurrentDictionary<string, long> _operationsByPath = new();
        private readonly ConcurrentDictionary<string, long> _failuresByExceptionType = new();

        /// <inheritdoc />
        public void RecordResolveStarted(string executionId, string stepId, string path)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _resolvedStartedCount);
            IncrementPath(path);
        }

        /// <inheritdoc />
        public void RecordResolveSuccess(string executionId, string stepId, string path)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _resolvedSuccessCount);
            IncrementPath(path);
        }

        /// <inheritdoc />
        public void RecordResolveMiss(string executionId, string stepId, string path)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _resolvedMissCount);
            IncrementPath(path);
        }

        /// <inheritdoc />
        public void RecordResolveFailed(string executionId, string stepId, string path, Exception exception)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _resolvedFailedCount);
            IncrementPath(path);

            var key = exception?.GetType().Name ?? "unknown";

            _failuresByExceptionType.AddOrUpdate(
                key,
                _ => 1,
                (_, current) => current + 1);
        }

        /// <summary>
        /// Gets the number of started resolver operations.
        /// </summary>
        public long ResolvedStartedCount => Interlocked.Read(ref _resolvedStartedCount);

        /// <summary>
        /// Gets the number of successful resolver operations.
        /// </summary>
        public long ResolvedSuccessCount => Interlocked.Read(ref _resolvedSuccessCount);

        /// <summary>
        /// Gets the number of resolver misses.
        /// </summary>
        public long ResolvedMissCount => Interlocked.Read(ref _resolvedMissCount);

        /// <summary>
        /// Gets the number of failed resolver operations.
        /// </summary>
        public long ResolvedFailedCount => Interlocked.Read(ref _resolvedFailedCount);

        /// <summary>
        /// Gets resolver operations grouped by path.
        /// </summary>
        public IReadOnlyDictionary<string, long> OperationsByPath => _operationsByPath;

        /// <summary>
        /// Gets resolver failures grouped by exception type.
        /// </summary>
        public IReadOnlyDictionary<string, long> FailuresByExceptionType => _failuresByExceptionType;

        private void IncrementPath(string path)
        {
            var key = Normalize(path);

            _operationsByPath.AddOrUpdate(
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