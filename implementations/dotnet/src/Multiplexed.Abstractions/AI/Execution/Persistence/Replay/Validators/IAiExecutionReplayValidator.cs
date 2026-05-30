using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Validators
{
    /// <summary>
    /// Validates whether a persisted AI execution can be replayed deterministically.
    /// </summary>
    public interface IAiExecutionReplayValidator
    {
        /// <summary>
        /// Validates replay determinism for the supplied execution record and state.
        /// </summary>
        Task<AiExecutionReplayReport> ValidateAsync(
            AiExecutionReplayRequest request,
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}