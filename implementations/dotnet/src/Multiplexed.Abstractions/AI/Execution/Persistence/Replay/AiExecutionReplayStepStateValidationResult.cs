namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Result of replay step state validation.
    /// </summary>
    public sealed class AiExecutionReplayStepStateValidationResult
    {
        /// <summary>
        /// Gets whether all replay step states are valid.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets validation issues discovered during step state validation.
        /// </summary>
        public IReadOnlyCollection<AiExecutionReplayIssue> Issues { get; init; } =
            Array.Empty<AiExecutionReplayIssue>();
    }
}