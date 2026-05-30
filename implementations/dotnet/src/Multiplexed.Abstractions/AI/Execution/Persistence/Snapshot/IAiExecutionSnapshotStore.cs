using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot
{
    /// <summary>
    /// Defines persistence operations for durable AI execution snapshots.
    ///
    /// This store is intended for:
    /// - Debugging
    /// - Audit
    /// - Replay support
    /// - Post-mortem analysis
    ///
    /// It is not the authoritative source of truth for distributed execution
    /// coordination. The runtime store remains responsible for claims, retries,
    /// leases, and finalization semantics.
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The serializable external context snapshot type associated with the execution.
    /// </typeparam>
    public interface IAiExecutionSnapshotStore<TContextSnapshot>
    {
        /// <summary>
        /// Saves or replaces the full snapshot for the specified execution.
        /// </summary>
        /// <param name="snapshot">The snapshot to persist.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task UpsertAsync(
            AiExecutionSnapshotDocument<TContextSnapshot> snapshot,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the full snapshot for the specified execution identifier.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The snapshot if found; otherwise null.</returns>
        Task<AiExecutionSnapshotDocument<TContextSnapshot>?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the snapshot for the specified execution identifier.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task DeleteAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}