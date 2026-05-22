using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Stores;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Persistence
{
    /// <summary>
    /// Loads persisted enterprise runtime execution artifacts.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionPersistenceLoader
    {
        /// <summary>
        /// Loads the persisted execution record.
        /// </summary>
        /// <param name="dagStore">
        /// The DAG execution store.
        /// </param>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <returns>
        /// The persisted execution record.
        /// </returns>
        public async Task<AiExecutionRecord> LoadPersistedRecordAsync(
            IAiDagExecutionStore dagStore,
            string executionId)
        {
            ArgumentNullException.ThrowIfNull(
                dagStore);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                executionId);

            var persistedRecord = await dagStore.GetRecordAsync(
                    executionId)
                .ConfigureAwait(false);

            if (persistedRecord is null)
            {
                throw new InvalidOperationException(
                    $"Persisted execution record '{executionId}' was not found.");
            }

            return persistedRecord;
        }

        /// <summary>
        /// Loads the persisted execution state.
        /// </summary>
        /// <param name="dagStore">
        /// The DAG execution store.
        /// </param>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <returns>
        /// The persisted execution state.
        /// </returns>
        public async Task<AiExecutionState> LoadPersistedStateAsync(
            IAiDagExecutionStore dagStore,
            string executionId)
        {
            ArgumentNullException.ThrowIfNull(
                dagStore);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                executionId);

            var persistedState = await dagStore.GetStateAsync(
                    executionId)
                .ConfigureAwait(false);

            if (persistedState is null)
            {
                throw new InvalidOperationException(
                    $"Persisted execution state '{executionId}' was not found.");
            }

            return persistedState;
        }
    }
}