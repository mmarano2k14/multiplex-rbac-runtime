using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Control
{
    /// <summary>
    /// Provides runtime-facing execution control checks used before advancing execution work.
    /// </summary>
    /// <remarks>
    /// This gate is intentionally small. Runtime components such as distributed runners,
    /// batch runners, step claim services, and workers should use this abstraction instead
    /// of embedding pause, resume, cancellation, or human-input logic directly.
    /// </remarks>
    public interface IAiExecutionControlGate
    {
        /// <summary>
        /// Checks whether the execution may claim or advance work.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A control decision describing whether execution work may advance.</returns>
        Task<AiExecutionControlDecision> CheckBeforeAdvanceAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}