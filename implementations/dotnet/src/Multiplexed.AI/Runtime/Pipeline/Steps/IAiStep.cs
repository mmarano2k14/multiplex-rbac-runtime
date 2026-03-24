using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.Pipeline.Steps
{
    public interface IAiStep
    {
        /// <summary>
        /// Unique name of the step (used for logging, tracing, debugging).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Executes the step logic.
        /// </summary>
        /// <param name="context">Shared pipeline context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Step result.</returns>
        Task<AiStepResult> ExecuteAsync(
            AiStepContext context,
            CancellationToken cancellationToken = default);
    }
}
