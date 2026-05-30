namespace Multiplexed.Abstractions.AI.Observability.Metrics
{
    /// <summary>
    /// No-operation implementation of <see cref="IAiRuntimeMetricWriter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This writer intentionally ignores all metric observations.
    /// </para>
    ///
    /// <para>
    /// It is useful for unit tests and compatibility constructors where the metric
    /// counter implementation should remain usable without a configured metric store.
    /// </para>
    /// </remarks>
    public sealed class NoOpAiRuntimeMetricWriter : IAiRuntimeMetricWriter
    {
        /// <summary>
        /// Gets the shared no-operation writer instance.
        /// </summary>
        public static NoOpAiRuntimeMetricWriter Instance { get; } = new();

        private NoOpAiRuntimeMetricWriter()
        {
        }

        /// <inheritdoc />
        public Task RecordAsync(
            string category,
            string name,
            double value = 1,
            IReadOnlyDictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(category);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }
}