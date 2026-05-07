using Multiplexed.Abstractions.AI.Execution.State;

namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Provides contextual information required to execute claimed DAG steps.
    /// </summary>
    public sealed class AiDagStepExecutionContext
    {
        /// <summary>
        /// Gets the unique execution identifier of the current DAG execution.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the current AI execution state.
        /// </summary>
        public required AiExecutionState State { get; init; }

        /// <summary>
        /// Gets the maximum number of steps that may be executed concurrently
        /// for this execution batch.
        /// </summary>
        public int MaxDegreeOfParallelism { get; init; } = 1;
    }
}