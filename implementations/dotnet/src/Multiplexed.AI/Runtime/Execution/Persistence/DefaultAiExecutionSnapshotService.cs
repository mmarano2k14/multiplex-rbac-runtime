using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.AI.Runtime.Execution.Persistence.Normalization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.Persistence
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionSnapshotService{TContextSnapshot}"/>.
    ///
    /// RESPONSIBILITIES:
    /// - Create a snapshot document using the snapshot factory
    /// - Persist the snapshot using the configured snapshot store
    ///
    /// DESIGN:
    /// - This service acts as a thin orchestration layer
    /// - It centralizes snapshot persistence logic outside of the execution engine
    /// - It ensures that snapshot persistence remains optional and non-intrusive
    ///
    /// RESILIENCE:
    /// - Snapshot persistence is best-effort
    /// - Failures are caught and should not affect execution flow
    ///
    /// EXTENSIBILITY:
    /// - Can later be extended to:
    ///   - append execution events
    ///   - support versioned snapshots
    ///   - integrate audit pipelines or event streams
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

            // Only persist terminal executions
            if (!record.IsTerminal)
                return;

            try
            {
                var snapshot = _factory.Create(
                    record,
                    state,
                    contextKey,
                    contextSnapshot);

                snapshot = AiExecutionSnapshotNormalizer.Normalize(snapshot);

                await _store.UpsertAsync(snapshot, cancellationToken);
            }
            catch
            {
                // INTENTIONALLY SWALLOWED
                // Snapshot persistence must never impact execution reliability
                // Logging can be added here if needed
            }
        }
    }
}