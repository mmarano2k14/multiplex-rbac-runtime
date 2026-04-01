using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution;

namespace Multiplexed.AI.Stores
{
    /// <summary>
    /// Defines the contract for persisting AI execution orchestration and execution state.
    /// 
    /// This store is responsible for:
    /// - storing execution records
    /// - storing execution state
    /// - retrieving both independently
    /// - updating both safely using optimistic concurrency
    /// - saving record/state independently when needed
    /// - deleting record/state independently for coordinated cleanup workflows
    /// </summary>
    public interface IAiExecutionStore
    {
        /// <summary>
        /// Creates a new execution record and its associated execution state.
        /// </summary>
        /// <param name="record">Execution orchestration record.</param>
        /// <param name="state">Execution working state.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CreateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an execution record by execution identifier.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The execution record if found; otherwise null.</returns>
        Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an execution state by execution identifier.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The execution state if found; otherwise null.</returns>
        Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to update an execution record and its state using optimistic concurrency.
        /// 
        /// The update must only succeed if the current persisted ExecutionStepKey
        /// matches the expected step key.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="expectedStepKey">Expected execution step key.</param>
        /// <param name="record">Updated execution record.</param>
        /// <param name="state">Updated execution state.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if update succeeded; otherwise false.</returns>
        Task<bool> TryUpdateAsync(
            string executionId,
            string expectedStepKey,
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves an execution record independently.
        /// 
        /// This is useful for record-only persistence outside the paired optimistic update flow.
        /// </summary>
        /// <param name="record">Execution orchestration record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveRecordAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves an execution state independently.
        /// 
        /// This is useful for state-only persistence outside the paired optimistic update flow.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="state">Execution working state.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveStateAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an execution record by execution identifier.
        /// 
        /// This method must be safe to call even if the record does not exist.
        /// Missing data should be treated as a normal idempotent cleanup case.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an execution state by execution identifier.
        /// 
        /// This method must be safe to call even if the state does not exist.
        /// Missing data should be treated as a normal idempotent cleanup case.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteStateAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}