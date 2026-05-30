using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// In-memory replay metadata store used for tests, demos, and local runtime scenarios.
    /// </summary>
    public sealed class InMemoryAiExecutionReplayMetadataStore : IAiExecutionReplayMetadataStore
    {
        private readonly ConcurrentDictionary<string, AiExecutionReplayMetadata> _metadata = new(
            StringComparer.Ordinal);

        /// <inheritdoc />
        public Task<AiExecutionReplayMetadata?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                return Task.FromResult<AiExecutionReplayMetadata?>(null);
            }

            _metadata.TryGetValue(
                executionId,
                out var metadata);

            return Task.FromResult(metadata);
        }

        /// <inheritdoc />
        public Task SaveAsync(
            AiExecutionReplayMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            _metadata[metadata.ExecutionId] = metadata;

            return Task.CompletedTask;
        }
    }
}