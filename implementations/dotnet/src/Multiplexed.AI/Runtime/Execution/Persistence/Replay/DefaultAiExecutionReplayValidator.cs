using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay.Fingerprint;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Validates replay determinism by comparing the reconstructed execution
    /// fingerprint against persisted replay metadata and validating payload references.
    /// </summary>
    public sealed class DefaultAiExecutionReplayValidator : IAiExecutionReplayValidator
    {
        private readonly IAiExecutionReplayMetadataStore _metadataStore;
        private readonly IAiExecutionReplayFingerprintBuilder _fingerprintBuilder;
        private readonly IAiExecutionReplayPayloadValidator _payloadValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionReplayValidator"/> class.
        /// </summary>
        /// <param name="metadataStore">
        /// Replay metadata store containing persisted terminal fingerprints.
        /// </param>
        /// <param name="fingerprintBuilder">
        /// Fingerprint builder used to reconstruct deterministic execution fingerprints.
        /// </param>
        /// <param name="payloadValidator">
        /// Payload validator used to verify replay payload references.
        /// </param>
        public DefaultAiExecutionReplayValidator(
            IAiExecutionReplayMetadataStore metadataStore,
            IAiExecutionReplayFingerprintBuilder fingerprintBuilder,
            IAiExecutionReplayPayloadValidator payloadValidator)
        {
            _metadataStore = metadataStore
                ?? throw new ArgumentNullException(nameof(metadataStore));

            _fingerprintBuilder = fingerprintBuilder
                ?? throw new ArgumentNullException(nameof(fingerprintBuilder));

            _payloadValidator = payloadValidator
                ?? throw new ArgumentNullException(nameof(payloadValidator));
        }

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

            var payloadValidation = await _payloadValidator.ValidateAsync(
                    state,
                    cancellationToken)
                .ConfigureAwait(false);

            issues.AddRange(payloadValidation.Issues);

            var steps = state.Steps
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new AiExecutionReplayStepReport
                {
                    StepKey = x.Key,
                    Status = x.Value.Status.ToString(),
                    HasResult = x.Value.Result is not null,
                    IsExternalized =
                        x.Value.Result?.DataPayloads?.Values.Any(p => !p.IsInline) == true,
                    PayloadReferenceValid = payloadValidation.IsValid,
                    RetryCount = x.Value.RetryState?.RetryCount ?? 0,
                    RecoveryCount = x.Value.RecoveryCount
                })
                .ToArray();

            var replayValid =
                fingerprintMatches &&
                payloadValidation.IsValid;

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
                PayloadReferencesValid = payloadValidation.IsValid,

                ReplayValid = replayValid,

                PipelineName = record.PipelineName,
                Status = record.Status.ToString(),

                TotalSteps = state.Steps.Count,
                CompletedSteps = state.Steps.Values.Count(x => x.IsCompleted),
                FailedSteps = state.Steps.Values.Count(x => x.Status == AiStepExecutionStatus.Failed),
                WaitingForRetrySteps = state.Steps.Values.Count(x => x.Status == AiStepExecutionStatus.WaitingForRetry),
                RunningSteps = state.Steps.Values.Count(x => x.Status == AiStepExecutionStatus.Running),

                RetryCount = state.Steps.Values.Sum(x => x.RetryState?.RetryCount ?? 0),
                RecoveryCount = state.Steps.Values.Sum(x => x.RecoveryCount),

                FailureReason = replayValid
                    ? null
                    : !fingerprintFound
                        ? "Replay fingerprint metadata not found."
                        : !fingerprintMatches
                            ? "Replay fingerprint mismatch."
                            : "Replay payload reference validation failed.",

                Issues = issues,
                Steps = steps
            };
        }
    }
}