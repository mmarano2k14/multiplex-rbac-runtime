namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models
{
    /// <summary>
    /// Represents a request to replay, resume, or audit a persisted AI execution.
    /// </summary>
    public sealed class AiExecutionReplayRequest
    {
        /// <summary>
        /// Gets the execution identifier to replay.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the replay mode that controls how the persisted execution should be handled.
        /// </summary>
        public AiExecutionReplayMode Mode { get; init; } =
            AiExecutionReplayMode.ResumeIncomplete;

        /// <summary>
        /// Gets whether replay timeline events should be included in the replay report.
        /// </summary>
        public bool IncludeTimeline { get; init; }

        /// <summary>
        /// Gets whether execution-correlated decision ledger events should be included in the replay report.
        /// </summary>
        public bool IncludeLedgerEvents { get; init; }

        /// <summary>
        /// Gets whether per-step replay details should be included in the replay report.
        /// </summary>
        public bool IncludeStepDetails { get; init; } = true;

        /// <summary>
        /// Gets whether payload and archived payload references should be validated during replay.
        /// </summary>
        public bool ValidatePayloadReferences { get; init; } = true;
    }
}