using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Restores an execution from a persisted snapshot so it can resume through the normal runtime flow.
    /// </summary>
    public interface IAiExecutionReplayService
    {
        /// <summary>
        /// Replays the persisted snapshot for the specified execution identifier.
        /// </summary>
        /// <param name="executionId">The execution identifier to restore.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A structured replay result describing what happened.</returns>
        Task<AiExecutionReplayResult> ReplayAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}