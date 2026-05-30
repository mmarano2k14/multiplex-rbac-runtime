using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Metadata;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay.Fingerprint;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay.Metadata
{
    public sealed class DefaultAiExecutionReplayMetadataService : IAiExecutionReplayMetadataService
    {
        private readonly IAiExecutionReplayFingerprintBuilder _fingerprintBuilder;
        private readonly IAiExecutionReplayMetadataStore _metadataStore;

        public DefaultAiExecutionReplayMetadataService(
            IAiExecutionReplayFingerprintBuilder fingerprintBuilder,
            IAiExecutionReplayMetadataStore metadataStore)
        {
            _fingerprintBuilder = fingerprintBuilder;
            _metadataStore = metadataStore;
        }

        public async Task SaveTerminalFingerprintAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            var fingerprint = _fingerprintBuilder.Build(record, state);

            var metadata = AiExecutionReplayMetadataFactory.Create(
                fingerprint,
                record);

            await _metadataStore.SaveAsync(
                metadata,
                cancellationToken).ConfigureAwait(false);
        }
    }
}