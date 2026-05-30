using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.Abstractions.AI.Observability.Metrics.Store;
using System.Collections.Concurrent;

namespace Multiplexed.AI.Runtime.Observability.Metrics.Stores
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRuntimeMetricStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This store keeps runtime metric records in process memory. It is intended for
    /// tests, local diagnostics, demo scenarios, and lightweight development usage.
    /// </para>
    ///
    /// <para>
    /// Records stored by this implementation are not durable and are lost when the
    /// process exits.
    /// </para>
    /// </remarks>
    public sealed class InMemoryAiRuntimeMetricStore : IAiRuntimeMetricStore
    {
        private readonly ConcurrentQueue<AiRuntimeMetricRecord> _records = new();

        /// <inheritdoc />
        public Task AppendAsync(
            AiRuntimeMetricRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            cancellationToken.ThrowIfCancellationRequested();

            _records.Enqueue(record);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a snapshot of the metric records currently stored in memory.
        /// </summary>
        /// <returns>The metric records snapshot.</returns>
        public IReadOnlyCollection<AiRuntimeMetricRecord> GetSnapshot()
        {
            return _records.ToArray();
        }

        /// <summary>
        /// Clears all metric records currently stored in memory.
        /// </summary>
        public void Clear()
        {
            while (_records.TryDequeue(out _))
            {
            }
        }
    }
}