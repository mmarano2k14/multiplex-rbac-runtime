namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Validators
{
    /// <summary>
    /// Result of payload reference validation.
    /// </summary>
    public sealed class AiExecutionReplayPayloadValidationResult
    {
        public bool IsValid { get; init; }

        public IReadOnlyCollection<AiExecutionReplayIssue> Issues
        {
            get;
            init;
        }
        = Array.Empty<AiExecutionReplayIssue>();
    }
}