using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay.Fingerprint;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Default replay validator that compares the reconstructed execution fingerprint
    /// with the persisted terminal replay metadata.
    /// </summary>
    public sealed class DefaultAiExecutionReplayValidator : IAiExecutionReplayValidator
    {
        private readonly IAiExecutionReplayMetadataStore _metadataStore;
        private readonly IAiExecutionReplayFingerprintBuilder _fingerprintBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionReplayValidator"/> class.
        /// </summary>
        public DefaultAiExecutionReplayValidator(
            IAiExecutionReplayMetadataStore metadataStore,
            IAiExecutionReplayFingerprintBuilder fingerprintBuilder)
        {
            _metadataStore = metadataStore
                ?? throw new ArgumentNullException(nameof(metadataStore));

            _fingerprintBuilder = fingerprintBuilder
                ?? throw new ArgumentNullException(nameof(fingerprintBuilder));
        }

        /// <inheritdoc />
        /// <inheritdoc />
        public async Task<AiExecutionReplayReport> ValidateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            var metadata = await _metadataStore.GetAsync(
                    record.ExecutionId,
                    cancellationToken)
                .ConfigureAwait(false);

            var reconstructedFingerprint = _fingerprintBuilder.Build(
                record,
                state);

            var fingerprintFound =
                !string.IsNullOrWhiteSpace(metadata?.Fingerprint);

            var fingerprintMatches =
                fingerprintFound &&
                string.Equals(
                    metadata!.Fingerprint,
                    reconstructedFingerprint,
                    StringComparison.Ordinal);

            var issues = new List<AiExecutionReplayIssue>();

            if (!fingerprintFound)
            {
                issues.Add(new AiExecutionReplayIssue
                {
                    Code = "replay.fingerprint.missing",
                    Message = "Replay fingerprint metadata was not found."
                });
            }
            else if (!fingerprintMatches)
            {
                issues.Add(new AiExecutionReplayIssue
                {
                    Code = "replay.fingerprint.mismatch",
                    Message = "Replay fingerprint does not match the persisted terminal fingerprint."
                });
            }

            var steps = state.Steps
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new AiExecutionReplayStepReport
                {
                    StepKey = x.Key,
                    Status = x.Value.Status.ToString(),
                    HasResult = x.Value.Result is not null,
                    IsExternalized =
                        x.Value.Result?.DataPayloads?.Values.Any(p => !p.IsInline) == true,
                    PayloadReferenceValid = true,
                    RetryCount = x.Value.RetryState?.RetryCount ?? 0,
                    RecoveryCount = x.Value.RecoveryCount
                })
                .ToArray();

            return new AiExecutionReplayReport
            {
                ExecutionId = record.ExecutionId,
                ExecutionFound = true,
                SnapshotFound = true,
                FingerprintFound = fingerprintFound,
                OriginalFingerprint = metadata?.Fingerprint,
                ReconstructedFingerprint = reconstructedFingerprint,
                FingerprintMatches = fingerprintMatches,
                DependencyGraphValid = true,
                StepStateValid = true,
                PayloadReferencesValid = true,
                ReplayValid = fingerprintMatches,
                PipelineName = record.PipelineName,
                Status = record.Status.ToString(),
                TotalSteps = state.Steps.Count,
                CompletedSteps = state.Steps.Values.Count(x => x.IsCompleted),
                FailedSteps = state.Steps.Values.Count(x => x.Status == AiStepExecutionStatus.Failed),
                WaitingForRetrySteps = state.Steps.Values.Count(x => x.Status == AiStepExecutionStatus.WaitingForRetry),
                RunningSteps = state.Steps.Values.Count(x => x.Status == AiStepExecutionStatus.Running),
                RetryCount = state.Steps.Values.Sum(x => x.RetryState?.RetryCount ?? 0),
                RecoveryCount = state.Steps.Values.Sum(x => x.RecoveryCount),
                FailureReason = fingerprintMatches
                    ? null
                    : fingerprintFound
                        ? "Replay fingerprint mismatch."
                        : "Replay fingerprint metadata not found.",
                Issues = issues,
                Steps = steps
            };
        }
    }
}