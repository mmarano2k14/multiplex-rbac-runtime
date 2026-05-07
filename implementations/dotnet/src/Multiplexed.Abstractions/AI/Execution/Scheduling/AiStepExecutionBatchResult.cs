namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Represents the aggregated result of a DAG step batch execution.
    /// </summary>
    public sealed class AiStepExecutionBatchResult
    {
        /// <summary>
        /// Gets or sets the individual claimed step execution results.
        /// </summary>
        public IReadOnlyCollection<AiClaimedStepExecutionResult> Results { get; init; }
            = Array.Empty<AiClaimedStepExecutionResult>();

        /// <summary>
        /// Gets a value indicating whether the batch produced no execution results.
        /// </summary>
        public bool IsEmpty => Results.Count == 0;
    }
}