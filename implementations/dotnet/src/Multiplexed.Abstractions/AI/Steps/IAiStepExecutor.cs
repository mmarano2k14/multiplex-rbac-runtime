using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Steps
{
    /// <summary>
    /// Defines the contract for executing a single AI pipeline step.
    ///
    /// Implementations may provide additional behavior such as:
    /// - Retry handling
    /// - Idempotency guards
    /// - Execution metadata tracking
    /// - Telemetry and observability
    /// </summary>
    public interface IAiStepExecutor
    {
        /// <summary>
        /// Executes the specified step using the provided shared execution context.
        /// </summary>
        /// <param name="step">The step to execute.</param>
        /// <param name="context">The shared execution context.</param>
        /// <param name="cancellationToken">The cancellation token for the active execution.</param>
        /// <returns>The final step execution result.</returns>
        Task<AiStepResult> ExecuteAsync(
            IAiStep step,
            AiExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}