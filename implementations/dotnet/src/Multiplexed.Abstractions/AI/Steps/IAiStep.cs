using Multiplexed.Abstractions.AI.Execution;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI.Steps
{
    /// <summary>
    /// Represents a single executable step in the AI pipeline.
    /// Each step receives the shared execution context and returns
    /// a structured execution result.
    /// </summary>
    public interface IAiStep
    {
        /// <summary>
        /// Gets the unique step name used for tracing, logging, and diagnostics.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Executes the current step using the shared execution context.
        /// </summary>
        /// <param name="context">The current AI execution context.</param>
        /// <param name="cancellationToken">The cancellation token for the active execution.</param>
        /// <returns>The structured result of the step execution.</returns>
        Task<AiStepResult> ExecuteAsync(
            AiExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}
