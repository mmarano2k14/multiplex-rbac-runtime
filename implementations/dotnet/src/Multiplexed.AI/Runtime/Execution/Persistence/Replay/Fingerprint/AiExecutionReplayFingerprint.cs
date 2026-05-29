using System.Text.Json.Serialization;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay.Fingerprint
{
    /// <summary>
    /// Represents the deterministic replay fingerprint payload.
    /// </summary>
    internal sealed class AiExecutionReplayFingerprint
    {
        [JsonPropertyOrder(1)]
        public required string Status { get; init; }

        [JsonPropertyOrder(2)]
        public required bool IsTerminal { get; init; }

        [JsonPropertyOrder(3)]
        public required IReadOnlyList<string> CompletedSteps { get; init; }

        [JsonPropertyOrder(4)]
        public required IReadOnlyDictionary<string, string> StepStatuses { get; init; }

        [JsonPropertyOrder(5)]
        public required IReadOnlyDictionary<string, int> RetryCounts { get; init; }

        [JsonPropertyOrder(6)]
        public required IReadOnlyDictionary<string, int> RecoveryCounts { get; init; }
    }
}