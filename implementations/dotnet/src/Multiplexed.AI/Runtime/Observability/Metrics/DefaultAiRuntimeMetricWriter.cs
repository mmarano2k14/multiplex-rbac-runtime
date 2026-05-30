using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.Abstractions.AI.Observability.Metrics.Store;
using Multiplexed.AI.Runtime.Observability.Metrics.Helpers;

namespace Multiplexed.AI.Runtime.Observability.Metrics
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimeMetricWriter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This writer creates append-only runtime metric records, enriches them with a
    /// detached snapshot of the current runtime execution correlation context, and
    /// persists them through the configured metric store.
    /// </para>
    ///
    /// <para>
    /// Metric write failures are logged and swallowed by design so that observability
    /// persistence does not break runtime execution.
    /// </para>
    /// </remarks>
    public sealed class DefaultAiRuntimeMetricWriter : IAiRuntimeMetricWriter
    {
        private readonly IAiRuntimeMetricStore _store;
        private readonly IAiRuntimeCorrelationAccessor _correlationAccessor;
        private readonly ILogger<DefaultAiRuntimeMetricWriter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRuntimeMetricWriter"/> class.
        /// </summary>
        /// <param name="store">The configured runtime metric store.</param>
        /// <param name="correlationAccessor">The ambient runtime correlation accessor.</param>
        /// <param name="logger">The logger.</param>
        public DefaultAiRuntimeMetricWriter(
            IAiRuntimeMetricStore store,
            IAiRuntimeCorrelationAccessor correlationAccessor,
            ILogger<DefaultAiRuntimeMetricWriter> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _correlationAccessor = correlationAccessor ?? throw new ArgumentNullException(nameof(correlationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task RecordAsync(
            string category,
            string name,
            double value = 1,
            IReadOnlyDictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(category);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var record = new AiRuntimeMetricRecord
            {
                Category = category,
                Name = name,
                Value = value,
                TimestampUtc = DateTimeOffset.UtcNow,
                Tags = NormalizeTags(tags),
                Correlation = AiRuntimeMetricCorrelationSnapshotFactory.Create(
                    _correlationAccessor.Current)
            };

            try
            {
                await _store.AppendAsync(
                        record,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to persist AI runtime metric. Category='{Category}', Name='{Name}', Value='{Value}'.",
                    category,
                    name,
                    value);
            }
        }

        /// <summary>
        /// Normalizes metric tags into a mutable dictionary with non-null values.
        /// </summary>
        /// <param name="tags">The source tags.</param>
        /// <returns>The normalized metric tags.</returns>
        private static Dictionary<string, string> NormalizeTags(
            IReadOnlyDictionary<string, string>? tags)
        {
            var normalized = new Dictionary<string, string>(
                StringComparer.Ordinal);

            if (tags is null)
            {
                return normalized;
            }

            foreach (var item in tags)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                normalized[item.Key] = item.Value ?? string.Empty;
            }

            return normalized;
        }
    }
}