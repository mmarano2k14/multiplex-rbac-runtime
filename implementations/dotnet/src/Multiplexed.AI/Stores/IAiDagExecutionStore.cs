using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

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
    }
}