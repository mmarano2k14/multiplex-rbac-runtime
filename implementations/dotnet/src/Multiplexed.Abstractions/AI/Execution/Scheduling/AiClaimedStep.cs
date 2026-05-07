namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Represents a DAG step successfully claimed for execution.
    /// </summary>
    public sealed class AiClaimedStep
    {
        /// <summary>
        /// Gets the unique execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the unique step name.
        /// </summary>
        public required string StepName { get; init; }

        /// <summary>
        /// Gets the distributed claim token associated with the step claim.
        /// </summary>
        public required string ClaimToken { get; init; }
    }
}