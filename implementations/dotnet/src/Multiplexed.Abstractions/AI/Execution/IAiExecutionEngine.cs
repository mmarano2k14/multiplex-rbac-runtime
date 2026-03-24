namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Defines the orchestration contract for persisted AI execution workflows.
    ///
    /// Responsibilities:
    /// - Create a new durable AI execution
    /// - Execute a single next step of an existing execution
    /// - Execute the remaining workflow until a terminal state is reached
    /// </summary>
    public interface IAiExecutionEngine
    {
        /// <summary>
        /// Creates a new AI execution together with its persisted execution state.
        /// </summary>
        /// <param name="input">The initial input passed to the workflow.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The newly created execution record.</returns>
        Task<AiExecutionRecord> CreateAsync(
            string input,
            CancellationToken ct = default);

        /// <summary>
        /// Executes the next step of the specified AI execution.
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The updated execution record.</returns>
        Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken ct = default);

        /// <summary>
        /// Executes the remaining workflow until a terminal state is reached.
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The final execution record.</returns>
        Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken ct = default);
    }
}