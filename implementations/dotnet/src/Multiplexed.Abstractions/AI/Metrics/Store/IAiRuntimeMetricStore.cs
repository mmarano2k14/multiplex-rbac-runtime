namespace Multiplexed.Abstractions.AI.Metrics.Store
{
    /// <summary>
    /// Persists runtime metric records.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A metric store is append-only from the runtime point of view. It receives
    /// already-built metric records enriched with correlation data.
    /// </para>
    ///
    /// <para>
    /// Implementations may persist metrics in memory, MongoDB, a time-series backend,
    /// or any external observability system.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeMetricStore
    {
        /// <summary>
        /// Appends a runtime metric record to the store.
        /// </summary>
        /// <param name="record">The runtime metric record to append.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous append operation.</returns>
        Task AppendAsync(
            AiRuntimeMetricRecord record,
            CancellationToken cancellationToken = default);
    }
}