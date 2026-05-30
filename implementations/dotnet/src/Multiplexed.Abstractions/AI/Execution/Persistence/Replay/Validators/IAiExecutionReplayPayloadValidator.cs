namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Validators
{
    /// <summary>
    /// Validates payload references contained in replayed execution state.
    /// </summary>
    public interface IAiExecutionReplayPayloadValidator
    {
        /// <summary>
        /// Validates payload references contained in the execution state.
        /// </summary>
        Task<AiExecutionReplayPayloadValidationResult> ValidateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}