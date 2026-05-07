namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Represents a batch of claimed DAG steps ready to be executed.
    /// </summary>
    public sealed class AiStepExecutionBatch
    {
        /// <summary>
        /// Gets the claimed steps included in this batch.
        /// </summary>
        public required IReadOnlyList<AiClaimedStep> Steps { get; init; }

        /// <summary>
        /// Gets a value indicating whether the batch contains no claimed steps.
        /// </summary>
        public bool IsEmpty => Steps.Count == 0;
    }
}