using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.AI.Runtime.Execution.Persistence.Normalization;

namespace Multiplexed.AI.Runtime.Persistence
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionSnapshotService{TContextSnapshot}"/>.
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The serializable external context snapshot associated with the execution.
    /// </typeparam>
    public sealed class DefaultAiExecutionSnapshotService<TContextSnapshot>
        : IAiExecutionSnapshotService<TContextSnapshot>
    {
        private readonly IAiExecutionSnapshotFactory<TContextSnapshot> _factory;
        private readonly IAiExecutionSnapshotStore<TContextSnapshot> _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionSnapshotService{TContextSnapshot}"/> class.
        /// </summary>
        /// <param name="factory">Snapshot factory responsible for document creation.</param>
        /// <param name="store">Snapshot store responsible for persistence.</param>
        public DefaultAiExecutionSnapshotService(
            IAiExecutionSnapshotFactory<TContextSnapshot> factory,
            IAiExecutionSnapshotStore<TContextSnapshot> store)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <inheritdoc />
        public async Task TryPersistAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            string? contextKey,
            TContextSnapshot? contextSnapshot,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (!record.IsTerminal)
            {
                return;
            }

            try
            {
                var snapshot = _factory.Create(
                    record,
                    state,
                    contextKey,
                    contextSnapshot);

                snapshot = AiExecutionSnapshotNormalizer.Normalize(
                    snapshot);

                await PersistTerminalSnapshotAsync(
                    snapshot).ConfigureAwait(false);
            }
            catch
            {
                // INTENTIONALLY SWALLOWED
                // Snapshot persistence is best-effort and must not affect execution reliability.
            }
        }

        /// <summary>
        /// Persists a terminal execution snapshot as a durable replay artifact.
        /// </summary>
        /// <param name="snapshot">The terminal execution snapshot document.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// Terminal snapshots are replay-critical durability artifacts. They must not
        /// be cancelled by worker-loop or worker-group cancellation after terminal
        /// convergence has already been observed.
        /// </para>
        /// <para>
        /// For this reason, terminal snapshot persistence intentionally does not use
        /// the caller cancellation token. The service remains best-effort because any
        /// exception is still swallowed by <see cref="TryPersistAsync"/>.
        /// </para>
        /// </remarks>
        private async Task PersistTerminalSnapshotAsync(
            AiExecutionSnapshotDocument<TContextSnapshot> snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            await _store.UpsertAsync(
                snapshot,
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}