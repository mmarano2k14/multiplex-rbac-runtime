using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution.Control;

namespace Multiplexed.AI.Runtime.Execution.Control
{
    /// <summary>
    /// Default runtime-facing execution control gate.
    /// </summary>
    /// <remarks>
    /// This class keeps runtime components decoupled from the full execution control
    /// service. Runners and workers should depend on this gate when deciding whether
    /// an execution may continue claiming or advancing work.
    /// </remarks>
    public sealed class AiExecutionControlGate : IAiExecutionControlGate
    {
        private readonly IAiExecutionControlService _controlService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionControlGate"/> class.
        /// </summary>
        /// <param name="controlService">The execution control service.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="controlService"/> is null.
        /// </exception>
        public AiExecutionControlGate(IAiExecutionControlService controlService)
        {
            ArgumentNullException.ThrowIfNull(controlService);

            _controlService = controlService;
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlDecision> CheckBeforeAdvanceAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null, empty, or whitespace.", nameof(executionId));
            }

            var decision = await _controlService.CheckCanAdvanceAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (decision.ShouldCancel)
            {
                await _controlService.MarkCancelledAsync(
                        executionId,
                        requestedBy: "execution-control-gate",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return decision;
        }
    }
}