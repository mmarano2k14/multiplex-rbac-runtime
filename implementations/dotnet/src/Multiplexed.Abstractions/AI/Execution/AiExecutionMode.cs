namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Defines the execution strategy used by an AI pipeline.
    ///
    /// This determines how steps are scheduled and executed.
    /// </summary>
    public enum AiExecutionMode
    {
        /// <summary>
        /// Sequential execution.
        ///
        /// Steps are executed one after another using an index-based progression.
        /// </summary>
        Sequential = 0,

        /// <summary>
        /// Directed Acyclic Graph (DAG) execution.
        ///
        /// Steps are executed based on dependency satisfaction,
        /// allowing non-linear execution flow.
        /// </summary>
        Dag = 1
    }
}