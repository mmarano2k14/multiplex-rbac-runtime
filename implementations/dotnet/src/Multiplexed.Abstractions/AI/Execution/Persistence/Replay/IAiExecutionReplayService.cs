using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Provides replay and audit capabilities for persisted AI executions.
    /// </summary>
    public interface IAiExecutionReplayService
    {
        /// <summary>
        /// Replays or audits a persisted AI execution according to the requested replay mode.
        /// </summary>
        /// <param name="request">The replay request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The replay report.</returns>
        Task<AiExecutionReplayReport> ReplayAsync(
            AiExecutionReplayRequest request,
            CancellationToken cancellationToken = default);
    }
}