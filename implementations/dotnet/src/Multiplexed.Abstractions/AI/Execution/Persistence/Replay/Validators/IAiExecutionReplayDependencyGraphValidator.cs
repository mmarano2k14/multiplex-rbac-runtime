namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Validators
{
    /// <summary>
    /// Validates dependency graph consistency for replayed AI executions.
    /// </summary>
    public interface IAiExecutionReplayDependencyGraphValidator
    {
        /// <summary>
        /// Validates step dependency relationships contained in an execution state.
        /// </summary>
        Task<AiExecutionReplayDependencyGraphValidationResult> ValidateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}