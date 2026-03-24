using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Abstraction for persisting and loading AI execution records.
    /// </summary>
    public interface IAiExecutionStore
    {
        /// <summary>
        /// Creates a new execution record.
        /// </summary>
        Task CreateAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads an execution record by execution identifier.
        /// </summary>
        Task<AiExecutionRecord?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an execution record only if the expected step key matches.
        /// Used to protect step transitions with optimistic concurrency.
        /// </summary>
        Task<bool> TryUpdateAsync(
            string executionId,
            string expectedStepKey,
            AiExecutionRecord updatedRecord,
            CancellationToken cancellationToken = default);
    }
}