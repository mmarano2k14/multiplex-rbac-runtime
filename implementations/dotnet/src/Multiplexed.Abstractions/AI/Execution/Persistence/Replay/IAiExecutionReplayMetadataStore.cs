namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Stores deterministic replay metadata for completed AI executions.
    /// </summary>
    public interface IAiExecutionReplayMetadataStore
    {
        /// <summary>
        /// Gets replay metadata for an execution.
        /// </summary>
        Task<AiExecutionReplayMetadata?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves replay metadata for an execution.
        /// </summary>
        Task SaveAsync(
            AiExecutionReplayMetadata metadata,
            CancellationToken cancellationToken = default);
    }
}