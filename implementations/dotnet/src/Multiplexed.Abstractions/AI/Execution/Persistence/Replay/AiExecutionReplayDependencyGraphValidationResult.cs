namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Represents the result of replay dependency graph validation.
    /// </summary>
    public sealed class AiExecutionReplayDependencyGraphValidationResult
    {
        /// <summary>
        /// Gets whether the dependency graph is valid for replay.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets validation issues discovered during dependency graph validation.
        /// </summary>
        public IReadOnlyCollection<AiExecutionReplayIssue> Issues { get; init; } =
            Array.Empty<AiExecutionReplayIssue>();
    }
}