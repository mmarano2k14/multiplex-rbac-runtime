namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Represents persisted replay metadata for a completed AI execution.
    /// </summary>
    public sealed class AiExecutionReplayMetadata
    {
        /// <summary>
        /// Gets the execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the deterministic replay fingerprint.
        /// </summary>
        public required string Fingerprint { get; init; }

        /// <summary>
        /// Gets the replay fingerprint algorithm version.
        /// </summary>
        public string FingerprintVersion { get; init; } = "v1";

        /// <summary>
        /// Gets the UTC timestamp when the replay metadata was generated.
        /// </summary>
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    }
}