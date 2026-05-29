namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// No-op replay metadata store used when replay metadata persistence is not configured.
    /// </summary>
    public sealed class NoOpAiExecutionReplayMetadataStore : IAiExecutionReplayMetadataStore
    {
        /// <inheritdoc />
        public Task<AiExecutionReplayMetadata?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AiExecutionReplayMetadata?>(null);
        }

        /// <inheritdoc />
        public Task SaveAsync(
            AiExecutionReplayMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}