using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Payloads.Stores
{
    /// <summary>
    /// Stores and loads complete AI step states as external payloads.
    ///
    /// PURPOSE:
    /// - Support retention eviction without data loss.
    /// - Keep AiExecutionState.Steps small.
    /// - Allow evicted steps to remain recoverable from payload storage.
    ///
    /// IMPORTANT:
    /// - This is step-aware storage.
    /// - It wraps the lower-level IAiPayloadStore.
    /// - It does not decide retention.
    /// - It does not mutate AiExecutionState.
    /// </summary>
    public interface IAiStepPayloadStore
    {
        /// <summary>
        /// Saves a complete step state externally.
        /// </summary>
        Task<AiStoredPayload> SaveStepAsync(
            string executionId,
            string stepName,
            AiStepState step,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a previously stored step state.
        /// </summary>
        Task<AiStepState?> LoadStepAsync(
            string executionId,
            string stepName,
            AiStoredPayload payload,
            CancellationToken cancellationToken = default);
    }
}