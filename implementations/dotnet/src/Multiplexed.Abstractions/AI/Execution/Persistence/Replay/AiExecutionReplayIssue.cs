namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Represents a replay validation issue.
    /// </summary>
    public sealed class AiExecutionReplayIssue
    {
        /// <summary>
        /// Gets the issue code.
        /// </summary>
        public required string Code { get; init; }

        /// <summary>
        /// Gets the human-readable issue message.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Gets the related step key when the issue is step-specific.
        /// </summary>
        public string? StepKey { get; init; }
    }
}