using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Validates whether a persisted AI execution can be replayed deterministically.
    /// </summary>
    public interface IAiExecutionReplayValidator
    {
        /// <summary>
        /// Validates replay determinism for the supplied execution record and state.
        /// </summary>
        /// <param name="record">
        /// The execution record to validate.
        /// </param>
        /// <param name="state">
        /// The execution state to validate.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A replay report containing fingerprint comparison and validation status.
        /// </returns>
        Task<AiExecutionReplayReport> ValidateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}