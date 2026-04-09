using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Engine;

namespace Multiplexed.AI.Stores
{
    /// <summary>
    /// Distributed DAG execution store.
    ///
    /// This store enables:
    /// - step-level claiming
    /// - concurrent execution across workers
    /// - atomic step transitions
    /// - recovery of timed-out claims
    /// - administrative persistence and cleanup of distributed DAG execution state
    /// </summary>
    public interface IAiDagExecutionStore
    {
        Task CreateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);

        Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the global execution record independently.
        ///
        /// This is intended for administrative persistence or lifecycle updates
        /// that do not require re-writing all distributed step entries.
        /// </summary>
        Task SaveRecordAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the full distributed DAG state by replacing the current indexed steps
        /// for the specified execution.
        ///
        /// This is intended for administrative persistence, repair, or recovery flows,
        /// not for normal concurrent worker progression.
        /// </summary>
        Task SaveStateAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the global execution record.
        ///
        /// This operation should be idempotent.
        /// </summary>
        Task DeleteRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the global execution state.
        ///
        /// This operation should be idempotent.
        /// </summary>
        Task DeleteStateAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all indexed distributed DAG steps for the execution,
        /// including the step index itself.
        ///
        /// This operation should be idempotent.
        /// </summary>
        Task DeleteStepsAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the full distributed DAG execution bundle owned by this store.
        ///
        /// This includes the global execution record and all indexed step entries.
        /// This operation should be idempotent.
        /// </summary>
        Task DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        Task<ClaimedAiStep?> TryClaimNextReadyStepAsync(
            string executionId,
            string workerId,
            CancellationToken cancellationToken = default);

        Task<bool> TryCompleteStepAsync(
            string executionId,
            string stepName,
            string claimToken,
            AiStepResult result,
            CancellationToken cancellationToken = default);

        Task<bool> TryFailStepAsync(
            string executionId,
            string stepName,
            string claimToken,
            string? error,
            CancellationToken cancellationToken = default);

        Task<int> RecoverTimedOutStepsAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to atomically finalize the global execution record after convergence evaluation.
        ///
        /// This operation is terminal-only and must never be used to project non-terminal states
        /// such as <see cref="AiExecutionStatus.Running"/> or <see cref="AiExecutionStatus.Waiting"/>.
        ///
        /// GUARANTEES:
        /// - the execution record must exist
        /// - the <see cref="AiExecutionRecord.ExecutionStepKey"/> must match the expected value
        /// - terminal states are monotonic and cannot be downgraded
        /// - concurrent finalization attempts are resolved atomically
        ///
        /// RETURNS:
        /// - <c>true</c> if this worker successfully finalized the execution
        /// - <c>false</c> if another worker already finalized the execution,
        ///   if the optimistic execution key did not match,
        ///   or if the record was already terminal
        /// </summary>
        /// <param name="request">The terminal finalization request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if finalization succeeded; otherwise false.</returns>
        Task<bool> TryFinalizeExecutionAsync(
            AiDagExecutionFinalizationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores an execution record and state without concurrency checks.
        /// 
        /// PURPOSE:
        /// - Used by replay systems to rehydrate execution state after crash/restart
        /// - Bypasses optimistic concurrency mechanisms
        /// 
        /// BEHAVIOR:
        /// - Must overwrite existing record/state if present
        /// - Must be idempotent
        /// </summary>
        Task RestoreAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}