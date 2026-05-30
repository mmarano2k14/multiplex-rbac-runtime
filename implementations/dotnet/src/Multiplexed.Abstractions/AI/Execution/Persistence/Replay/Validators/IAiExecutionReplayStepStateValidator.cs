namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Validators
{
    /// <summary>
    /// Validates step state consistency for replayed executions.
    /// </summary>
    public interface IAiExecutionReplayStepStateValidator
    {
        /// <summary>
        /// Validates execution step states.
        /// </summary>
        Task<AiExecutionReplayStepStateValidationResult> ValidateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}