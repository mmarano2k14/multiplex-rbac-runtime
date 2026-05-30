using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.Abstractions.AI.Observability.Metrics.Store;

namespace Multiplexed.AI.Runtime.Observability.Metrics.Stores
{
    /// <summary>
    /// No-operation implementation of <see cref="IAiRuntimeMetricStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This store intentionally ignores all metric records. It is used when metric
    /// persistence is disabled.
    /// </para>
    /// </remarks>
    public sealed class NoOpAiRuntimeMetricStore : IAiRuntimeMetricStore
    {
        /// <inheritdoc />
        public Task AppendAsync(
            AiRuntimeMetricRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            return Task.CompletedTask;
        }
    }
}