namespace Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot
{
    /// <summary>
    /// Creates durable execution snapshot documents from runtime execution models.
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The serializable external context snapshot type associated with the execution.
    /// </typeparam>
    public interface IAiExecutionSnapshotFactory<TContextSnapshot>
    {
        /// <summary>
        /// Creates a snapshot document from the provided runtime models.
        /// </summary>
        /// <param name="record">The execution record.</param>
        /// <param name="state">The execution state.</param>
        /// <param name="contextKey">The stable runtime context key.</param>
        /// <param name="contextSnapshot">The external context snapshot.</param>
        /// <returns>A fully populated execution snapshot document.</returns>
        AiExecutionSnapshotDocument<TContextSnapshot> Create(
            AiExecutionRecord record,
            AiExecutionState state,
            string? contextKey,
            TContextSnapshot? contextSnapshot);
    }
}