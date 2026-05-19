using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Control
{
    /// <summary>
    /// Provides high-level control operations for durable AI executions.
    /// </summary>
    /// <remarks>
    /// This service coordinates operator, system, and human-in-the-loop control actions
    /// such as pause, resume, cancellation, and waiting for external input. It does not
    /// own DAG execution state. Instead, it operates on <see cref="AiExecutionControlState"/>
    /// through a durable control store.
    /// </remarks>
    public interface IAiExecutionControlService
    {
        /// <summary>
        /// Requests that an execution stop claiming new work and move toward a paused state.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="reason">The optional reason for the pause request.</param>
        /// <param name="requestedBy">The optional identity requesting the pause.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The updated control state.</returns>
        Task<AiExecutionControlState> PauseExecutionAsync(
            string executionId,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests that a paused, pausing, waiting, or resuming execution continue execution.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="requestedBy">The optional identity requesting the resume.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The updated control state.</returns>
        Task<AiExecutionControlState> ResumeExecutionAsync(
            string executionId,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests cooperative cancellation of an execution.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="reason">The optional reason for the cancellation request.</param>
        /// <param name="requestedBy">The optional identity requesting cancellation.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The updated control state.</returns>
        Task<AiExecutionControlState> CancelExecutionAsync(
            string executionId,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks an execution as waiting for external or human input.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="waitingKey">The stable waiting key used to correlate input.</param>
        /// <param name="waitingStepName">The optional step name that requested input.</param>
        /// <param name="reason">The optional reason for waiting.</param>
        /// <param name="requestedBy">The optional identity requesting the wait state.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The updated control state.</returns>
        Task<AiExecutionControlState> MarkWaitingForInputAsync(
            string executionId,
            string waitingKey,
            string? waitingStepName = null,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Submits external or human input for an execution that is waiting for input.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="waitingKey">The stable waiting key expected by the execution.</param>
        /// <param name="input">The input values to persist.</param>
        /// <param name="submittedBy">The optional identity submitting the input.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The updated control state.</returns>
        Task<AiExecutionControlState> SubmitHumanInputAsync(
            string executionId,
            string waitingKey,
            IReadOnlyDictionary<string, object?> input,
            string? submittedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates whether an execution may continue claiming or advancing work.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A control decision describing whether execution may advance.</returns>
        Task<AiExecutionControlDecision> CheckCanAdvanceAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a pausing execution as effectively paused after active claimed work has drained.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="requestedBy">The optional identity confirming the paused state.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The updated control state.</returns>
        /// <remarks>
        /// This method is intended for runtime integration. A pause request first enters
        /// <see cref="AiExecutionControlStatus.Pausing"/> so already claimed work can finish
        /// safely. Once the runtime observes that no active work remains, it may transition
        /// the control state to <see cref="AiExecutionControlStatus.Paused"/>.
        /// </remarks>
        Task<AiExecutionControlState> MarkPausedAsync(
            string executionId,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a resuming execution as effectively running after the runtime observes that work may advance again.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="requestedBy">The optional identity confirming the running state.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The updated control state.</returns>
        /// <remarks>
        /// This method is intended for runtime integration. Resume and human-input submission move
        /// the execution into <see cref="AiExecutionControlStatus.Resuming"/>. Once a runtime
        /// worker observes that the execution can advance again, it may normalize the control
        /// state back to <see cref="AiExecutionControlStatus.Running"/>.
        /// </remarks>
        Task<AiExecutionControlState> MarkRunningAsync(
            string executionId,
            string? requestedBy = null,
            CancellationToken cancellationToken = default);
    }
}