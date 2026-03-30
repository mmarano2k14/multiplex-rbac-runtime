using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Abstractions.AI.Steps
{
    /// <summary>
    /// Defines the contract for executing a single resolved AI pipeline step.
    ///
    /// Responsibilities:
    /// - Execute the concrete step implementation
    /// - Preserve access to resolved step metadata such as:
    ///   - step name
    ///   - step key
    ///   - declarative input
    ///   - declarative configuration
    /// - Support retry, observability, and execution decorators
    /// </summary>
    public interface IAiStepExecutor
    {
        /// <summary>
        /// Executes the specified resolved pipeline step using the provided shared execution context.
        /// </summary>
        /// <param name="step">The resolved pipeline step to execute.</param>
        /// <param name="context">The shared execution context.</param>
        /// <param name="cancellationToken">The cancellation token for the active execution.</param>
        /// <returns>The final step execution result.</returns>
        Task<AiStepResult> ExecuteAsync(
            ResolvedAiPipelineStep step,
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}