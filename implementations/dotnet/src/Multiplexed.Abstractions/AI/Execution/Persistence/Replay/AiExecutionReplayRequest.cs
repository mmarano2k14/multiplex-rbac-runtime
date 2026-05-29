namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Represents a request to replay or audit a persisted AI execution.
    /// </summary>
    public sealed class AiExecutionReplayRequest
    {
        /// <summary>
        /// Gets the execution identifier to replay.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the replay mode.
        /// </summary>
        public AiExecutionReplayMode Mode { get; init; } = AiExecutionReplayMode.ResumeIncomplete;

        /// <summary>
        /// Gets whether timeline events should be included in the replay report.
        /// </summary>
        public bool IncludeTimeline { get; init; }

        /// <summary>
        /// Gets whether ledger events should be included in the replay report.
        /// </summary>
        public bool IncludeLedgerEvents { get; init; }

        /// <summary>
        /// Gets whether step-level details should be included in the replay report.
        /// </summary>
        public bool IncludeStepDetails { get; init; }

        /// <summary>
        /// Gets whether payload and archived payload references should be validated.
        /// </summary>
        public bool ValidatePayloadReferences { get; init; } = true;
    }
}