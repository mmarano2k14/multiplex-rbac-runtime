using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot
{
    /// <summary>
    /// Defines a high-level service responsible for persisting execution snapshots.
    ///
    /// PURPOSE:
    /// - Encapsulates snapshot creation and storage behind a single abstraction
    /// - Keeps the execution engine decoupled from persistence details (Mongo, Redis, etc.)
    /// - Allows optional activation via dependency injection
    ///
    /// DESIGN:
    /// - This service is intentionally best-effort and must NEVER break execution flow
    /// - Snapshot persistence is only expected for terminal execution states
    /// - Internal implementations may enrich snapshots with events, metadata, or audit data
    ///
    /// USAGE:
    /// - Called by the execution engine once convergence reaches a terminal state
    /// - No-op if not registered in the container
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The serializable external context snapshot associated with the execution
    /// (e.g., RBAC execution context snapshot).
    /// </typeparam>
    public interface IAiExecutionSnapshotService<TContextSnapshot>
    {
        /// <summary>
        /// Attempts to persist a snapshot of the current execution state.
        ///
        /// BEHAVIOR:
        /// - Implementations should ignore non-terminal executions
        /// - Implementations must be resilient and avoid throwing unless critical
        ///
        /// </summary>
        /// <param name="record">The execution record (global projection).</param>
        /// <param name="state">The authoritative step-state snapshot.</param>
        /// <param name="contextKey">The associated context key (if any).</param>
        /// <param name="contextSnapshot">The serialized external context snapshot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task TryPersistAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            string? contextKey,
            TContextSnapshot? contextSnapshot,
            CancellationToken cancellationToken);
    }
}