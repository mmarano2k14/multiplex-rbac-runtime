using Multiplexed.Abstractions.AI.Observability.Tracing;

namespace Multiplexed.Abstractions.AI.Observability.Tracing.Store
{
    /// <summary>
    /// Stores runtime trace records produced by the AI runtime tracing pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations may store trace records in memory, persist them to MongoDB,
    /// write them to multiple backends, or ignore them entirely.
    /// </para>
    ///
    /// <para>
    /// Trace storage is observational only. A failure to persist traces must not
    /// change runtime execution semantics unless a future strict tracing mode is
    /// explicitly introduced.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeTraceStore
    {
        /// <summary>
        /// Appends a completed runtime trace record.
        /// </summary>
        /// <param name="record">The completed trace record.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the append operation.</returns>
        Task AppendAsync(
            AiTraceRecord record,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets trace records associated with the specified execution identifier.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The trace records for the execution.</returns>
        Task<IReadOnlyList<AiTraceRecord>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}