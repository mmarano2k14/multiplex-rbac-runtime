using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Validators;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay.Validators
{
    /// <summary>
    /// Default structural validator for replay payload references.
    /// </summary>
    public sealed class DefaultAiExecutionReplayPayloadValidator : IAiExecutionReplayPayloadValidator
    {
        /// <inheritdoc />
        public Task<AiExecutionReplayPayloadValidationResult> ValidateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            var issues = new List<AiExecutionReplayIssue>();

            foreach (var step in state.Steps.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                ValidateStepPayloads(
                    step.Key,
                    step.Value,
                    issues);
            }

            return Task.FromResult(new AiExecutionReplayPayloadValidationResult
            {
                IsValid = issues.Count == 0,
                Issues = issues
            });
        }

        /// <summary>
        /// Validates payload references for a single step.
        /// </summary>
        private static void ValidateStepPayloads(
            string stepKey,
            AiStepState step,
            ICollection<AiExecutionReplayIssue> issues)
        {
            if (step.Result?.DataPayloads is null)
            {
                return;
            }

            foreach (var payload in step.Result.DataPayloads.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                ValidatePayloadReference(
                    stepKey,
                    payload.Key,
                    payload.Value,
                    issues);
            }
        }

        /// <summary>
        /// Validates a single stored payload reference.
        /// </summary>
        private static void ValidatePayloadReference(
            string stepKey,
            string payloadKey,
            AiStoredPayload payload,
            ICollection<AiExecutionReplayIssue> issues)
        {
            if (payload.IsInline)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(payload.ArtifactId))
            {
                return;
            }

            issues.Add(new AiExecutionReplayIssue
            {
                Code = "replay.payload.reference.missing_artifact_id",
                StepKey = stepKey,
                Message = $"External payload reference '{payloadKey}' is missing an artifact id."
            });
        }
    }
}