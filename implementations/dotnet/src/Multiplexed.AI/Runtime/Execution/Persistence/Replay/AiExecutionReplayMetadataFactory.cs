using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Creates replay metadata from execution records and states.
    /// </summary>
    public static class AiExecutionReplayMetadataFactory
    {
        /// <summary>
        /// Creates replay metadata for a completed execution.
        /// </summary>
        public static AiExecutionReplayMetadata Create(
            string fingerprint,
            AiExecutionRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            return new AiExecutionReplayMetadata
            {
                ExecutionId = record.ExecutionId,
                Fingerprint = fingerprint,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }
}