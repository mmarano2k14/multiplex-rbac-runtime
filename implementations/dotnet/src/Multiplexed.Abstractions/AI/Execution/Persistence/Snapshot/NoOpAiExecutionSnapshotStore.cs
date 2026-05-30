using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot
{
    /// <summary>
    /// No-op implementation of <see cref="IAiExecutionSnapshotStore{TContextSnapshot}"/>.
    ///
    /// This implementation is useful when snapshot persistence is disabled
    /// through runtime configuration.
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The serializable external context snapshot type associated with the execution.
    /// </typeparam>
    public sealed class NoOpAiExecutionSnapshotStore<TContextSnapshot> : IAiExecutionSnapshotStore<TContextSnapshot>
    {
        /// <inheritdoc />
        public Task UpsertAsync(
            AiExecutionSnapshotDocument<TContextSnapshot> snapshot,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<AiExecutionSnapshotDocument<TContextSnapshot>?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AiExecutionSnapshotDocument<TContextSnapshot>?>(null);
        }

        /// <inheritdoc />
        public Task DeleteAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}