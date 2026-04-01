using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Convergence
{
    /// <summary>
    /// Represents the evaluated global convergence decision for a DAG execution.
    /// </summary>
    public sealed class AiDagExecutionConvergenceResult
    {
        /// <summary>
        /// Gets the evaluated execution status.
        /// </summary>
        public AiExecutionStatus Status { get; init; }

        /// <summary>
        /// Gets a value indicating whether the evaluated status is terminal.
        /// </summary>
        public bool IsTerminal =>
            Status is AiExecutionStatus.Completed
                or AiExecutionStatus.Failed
                or AiExecutionStatus.Cancelled;

        public static AiDagExecutionConvergenceResult Running() =>
            new() { Status = AiExecutionStatus.Running };

        public static AiDagExecutionConvergenceResult Waiting() =>
            new() { Status = AiExecutionStatus.Waiting };

        public static AiDagExecutionConvergenceResult Completed() =>
            new() { Status = AiExecutionStatus.Completed };

        public static AiDagExecutionConvergenceResult Failed() =>
            new() { Status = AiExecutionStatus.Failed };

        public static AiDagExecutionConvergenceResult Cancelled() =>
            new() { Status = AiExecutionStatus.Cancelled };
    }
}
