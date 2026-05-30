using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Executes replay behavior after a persisted execution has been loaded and validated.
    /// </summary>
    public interface IAiExecutionReplayExecutor
    {
        /// <summary>
        /// Executes the requested replay mode.
        /// </summary>
        Task<AiExecutionReplayReport> ExecuteAsync(
            AiExecutionReplayRequest request,
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}