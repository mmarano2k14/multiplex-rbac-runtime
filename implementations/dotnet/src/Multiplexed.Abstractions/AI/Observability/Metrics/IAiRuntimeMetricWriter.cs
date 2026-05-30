namespace Multiplexed.Abstractions.AI.Observability.Metrics
{
    /// <summary>
    /// Writes runtime metric observations to the configured runtime metric store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The writer is responsible for creating append-only metric records and attaching
    /// the current runtime execution correlation context.
    /// </para>
    ///
    /// <para>
    /// Runtime metric domain services should depend on this writer instead of depending
    /// directly on a concrete store implementation.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeMetricWriter
    {
        /// <summary>
        /// Records one runtime metric observation.
        /// </summary>
        /// <param name="category">The metric category.</param>
        /// <param name="name">The metric name.</param>
        /// <param name="value">The metric numeric value.</param>
        /// <param name="tags">The optional metric tags.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        Task RecordAsync(
            string category,
            string name,
            double value = 1,
            IReadOnlyDictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default);
    }
}