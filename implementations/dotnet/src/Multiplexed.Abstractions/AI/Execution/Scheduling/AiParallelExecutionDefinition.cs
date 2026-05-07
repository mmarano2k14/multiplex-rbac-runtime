namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Defines pipeline-level parallel execution behavior for DAG execution.
    /// </summary>
    /// <remarks>
    /// This definition is intended to be resolved from pipeline JSON configuration.
    /// It controls how many claimed DAG steps may be executed concurrently for a
    /// single execution attempt.
    /// </remarks>
    public sealed class AiParallelExecutionDefinition
    {
        /// <summary>
        /// Gets or sets a value indicating whether parallel DAG execution is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of claimed steps that may execute concurrently.
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 1;
    }
}