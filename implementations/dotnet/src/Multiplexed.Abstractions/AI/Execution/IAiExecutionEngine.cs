namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Defines the orchestration contract for persisted AI executions.
    ///
    /// Architecture summary:
    /// IAiExecutionEngine
    ///     -> IAiExecutionStore
    ///     -> IAiPipelineExecutor
    ///
    /// Responsibilities:
    /// - Create durable executions
    /// - Execute one step at a time
    /// - Execute the remaining steps until terminal state
    /// - Persist orchestration transitions safely
    ///
    /// The engine owns orchestration and persistence.
    /// It does not resolve pipelines directly and does not execute step logic directly.
    /// </summary>
    public interface IAiExecutionEngine
    {
        /// <summary>
        /// Creates a new durable AI execution using the default pipeline behavior.
        /// </summary>
        /// <param name="input">The initial workflow input.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created execution record.</returns>
        Task<AiExecutionRecord> CreateAsync(
            string input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new durable AI execution for the specified pipeline.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name associated with the execution.</param>
        /// <param name="input">The initial workflow input.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created execution record.</returns>
        Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new durable AI execution for the specified pipeline.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name associated with the execution.</param>
        /// <param name="input">The initial workflow input.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created execution record.</returns>
        public abstract Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            IDictionary<string, object?> input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the next step of an existing AI execution.
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated execution record.</returns>
        Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the remaining workflow until a terminal state is reached.
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The final execution record.</returns>
        Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes multiple ready DAG steps for an existing AI execution.
        /// </summary>
        /// <param name="executionId">
        /// The unique execution identifier.
        /// </param>
        /// <param name="maxSteps">
        /// The maximum number of ready steps to execute.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The updated execution record.
        /// </returns>
        Task<AiExecutionRecord> ExecuteBatchAsync(
            string executionId,
            int maxSteps,
            CancellationToken cancellationToken = default);
    }
}