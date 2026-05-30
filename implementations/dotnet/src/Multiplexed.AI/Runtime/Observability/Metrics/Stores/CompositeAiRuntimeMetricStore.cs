using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.Abstractions.AI.Observability.Metrics.Store;

namespace Multiplexed.AI.Runtime.Observability.Metrics.Stores
{
    /// <summary>
    /// Composite implementation of <see cref="IAiRuntimeMetricStore"/> that appends
    /// metric records to multiple underlying stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This store is useful when metrics must be available in memory for live diagnostics
    /// while also being persisted durably to an external store such as MongoDB.
    /// </para>
    ///
    /// <para>
    /// The same metric record is appended to each configured store. Store implementations
    /// should treat records as append-only observations.
    /// </para>
    /// </remarks>
    public sealed class CompositeAiRuntimeMetricStore : IAiRuntimeMetricStore
    {
        private readonly IReadOnlyCollection<IAiRuntimeMetricStore> _stores;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAiRuntimeMetricStore"/> class.
        /// </summary>
        /// <param name="stores">The metric stores to append to.</param>
        public CompositeAiRuntimeMetricStore(
            IEnumerable<IAiRuntimeMetricStore> stores)
        {
            ArgumentNullException.ThrowIfNull(stores);

            _stores = stores
                .Where(store => store is not null)
                .ToArray();

            if (_stores.Count == 0)
            {
                throw new ArgumentException(
                    "At least one metric store must be provided.",
                    nameof(stores));
            }
        }

        /// <inheritdoc />
        public async Task AppendAsync(
            AiRuntimeMetricRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            foreach (var store in _stores)
            {
                await store.AppendAsync(
                        record,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}