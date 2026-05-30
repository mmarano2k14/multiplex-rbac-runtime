using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay.Models
{
    public sealed class AiExecutionReplayResult
    {
        public string ExecutionId { get; init; } = string.Empty;

        public bool SnapshotFound { get; init; }

        public bool IsValid { get; init; }

        public bool AlreadyExists { get; init; }

        public bool Restored { get; init; }

        public AiExecutionStatus? Status { get; init; }

        public AiExecutionStatus? ExistingStatus { get; init; }

        public AiExecutionStatus? RestoredStatus { get; init; }

        public int StepsCount { get; init; }

        public DateTime ReplayPerformedAtUtc { get; init; }

        public string? Message { get; init; }

        public string? Reason { get; init; }
    }
}