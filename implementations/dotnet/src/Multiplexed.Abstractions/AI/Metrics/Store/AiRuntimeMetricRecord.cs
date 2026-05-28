using Multiplexed.Abstractions.AI.Observability.Context;

namespace Multiplexed.Abstractions.AI.Metrics.Store
{
    /// <summary>
    /// Represents one runtime metric observation enriched with runtime correlation data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A runtime metric record is an append-only observation produced by the runtime
    /// metrics layer.
    /// </para>
    ///
    /// <para>
    /// The record can be persisted to an external store such as MongoDB and later queried
    /// by execution, run, pipeline, runtime instance, worker, category, name, or tags.
    /// </para>
    ///
    /// <para>
    /// The <see cref="Correlation"/> property uses the shared
    /// <see cref="AiRuntimeExecutionCorrelationContext"/> model. It should contain a
    /// detached snapshot of the ambient correlation context, not the mutable ambient
    /// instance itself.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeMetricRecord
    {
        /// <summary>
        /// Gets or sets the unique metric record identifier.
        /// </summary>
        public string MetricId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the UTC timestamp when the metric was recorded.
        /// </summary>
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the metric category.
        /// </summary>
        /// <example>
        /// Worker, Execution, Retention, Storage, Resolver, Policy.
        /// </example>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metric name.
        /// </summary>
        /// <example>
        /// worker.started, execution.completed, retention.triggered.
        /// </example>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric metric value.
        /// </summary>
        public double Value { get; set; } = 1;

        /// <summary>
        /// Gets or sets the metric tags.
        /// </summary>
        /// <remarks>
        /// Tag values must never be null. Use <see cref="string.Empty"/> when a value is
        /// unavailable.
        /// </remarks>
        public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the runtime execution correlation snapshot associated with this metric.
        /// </summary>
        public AiRuntimeExecutionCorrelationContext Correlation { get; set; } = new()
        {
            CorrelationId = string.Empty
        };
    }
}