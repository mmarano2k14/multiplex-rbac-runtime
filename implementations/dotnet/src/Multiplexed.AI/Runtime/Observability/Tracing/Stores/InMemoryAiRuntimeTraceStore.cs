using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.Observability.Tracing;
using Multiplexed.Abstractions.AI.Observability.Tracing.Store;

namespace Multiplexed.AI.Runtime.Observability.Tracing.Stores
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRuntimeTraceStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This store keeps completed trace records in process memory and groups them
    /// by resolved execution identifier.
    /// </para>
    ///
    /// <para>
    /// It is intended for tests, local diagnostics, live runtime inspection, and
    /// early UI work. It is not durable.
    /// </para>
    /// </remarks>
    public sealed class InMemoryAiRuntimeTraceStore : IAiRuntimeTraceStore
    {
        private readonly ConcurrentDictionary<string, List<AiTraceRecord>> _recordsByExecutionId =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task AppendAsync(
            AiTraceRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            cancellationToken.ThrowIfCancellationRequested();

            var executionId = ResolveExecutionId(record);

            if (string.IsNullOrWhiteSpace(executionId))
            {
                return Task.CompletedTask;
            }

            var records = _recordsByExecutionId.GetOrAdd(
                executionId,
                _ => new List<AiTraceRecord>());

            lock (records)
            {
                records.Add(record);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiTraceRecord>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            cancellationToken.ThrowIfCancellationRequested();

            if (!_recordsByExecutionId.TryGetValue(executionId, out var records))
            {
                return Task.FromResult<IReadOnlyList<AiTraceRecord>>(
                    Array.Empty<AiTraceRecord>());
            }

            lock (records)
            {
                return Task.FromResult<IReadOnlyList<AiTraceRecord>>(
                    records
                        .OrderBy(record => record.StartedAtUtc)
                        .ToArray());
            }
        }

        /// <summary>
        /// Gets a snapshot of all stored trace records.
        /// </summary>
        /// <returns>The stored trace records.</returns>
        public IReadOnlyList<AiTraceRecord> Snapshot()
        {
            return _recordsByExecutionId.Values
                .SelectMany(records =>
                {
                    lock (records)
                    {
                        return records.ToArray();
                    }
                })
                .OrderBy(record => record.StartedAtUtc)
                .ToArray();
        }

        private static string? ResolveExecutionId(
            AiTraceRecord record)
        {
            return FirstNonEmpty(
                record.ExecutionId,
                record.Correlation?.Runtime?.ExecutionId,
                record.Correlation?.Runtime?.RunId,
                record.Correlation?.Runtime?.CorrelationId);
        }

        private static string? FirstNonEmpty(
            params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }
    }
}