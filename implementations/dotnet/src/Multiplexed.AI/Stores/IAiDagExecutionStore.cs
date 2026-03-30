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