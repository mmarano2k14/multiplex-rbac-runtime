using Multiplexed.Abstractions.AI.Execution;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay.Fingerprint
{
    /// <summary>
    /// Default deterministic fingerprint builder for replay validation.
    /// </summary>
    public sealed class DefaultAiExecutionReplayFingerprintBuilder : IAiExecutionReplayFingerprintBuilder
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        /// <inheritdoc />
        public string Build(
            AiExecutionRecord record,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            var steps = state.Steps ?? new Dictionary<string, AiStepState>();

            var fingerprint = new AiExecutionReplayFingerprint
            {
                Status = record.Status.ToString(),
                IsTerminal = record.IsTerminal,
                CompletedSteps = steps
                    .Where(x => x.Value.Status == AiStepExecutionStatus.Completed)
                    .Select(x => x.Key)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray(),
                StepStatuses = steps
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value.Status.ToString(),
                        StringComparer.Ordinal),
                RetryCounts = steps
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value.RetryState?.RetryCount ?? 0,
                        StringComparer.Ordinal),
                RecoveryCounts = steps
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value.RecoveryCount,
                        StringComparer.Ordinal)
            };

            var json = JsonSerializer.Serialize(
                fingerprint,
                JsonOptions);

            var bytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(json));

            return Convert.ToHexString(bytes);
        }
    }
}