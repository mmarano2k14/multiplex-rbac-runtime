using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Orchestrates the execution of one or more claimed DAG steps for a single AI execution.
    /// </summary>
    public interface IAiDagStepExecutionOrchestrator
    {
        /// <summary>
        /// Executes a batch of already-claimed DAG steps.
        /// </summary>
        /// <param name="context">
        /// The execution context containing the current AI execution state and runtime metadata.
        /// </param>
        /// <param name="batch">
        /// The batch of claimed DAG steps to execute.
        /// </param>
        /// <param name="executeStepAsync">
        /// The delegate responsible for executing an individual claimed DAG step.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A batch execution result containing the individual step execution results.
        /// </returns>
        Task<AiStepExecutionBatchResult> ExecuteAsync(
            AiDagStepExecutionContext context,
            AiStepExecutionBatch batch,
            AiStepExecutionDelegate executeStepAsync,
            CancellationToken cancellationToken = default);
    }
}