using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Control
{
    /// <summary>
    /// Provides durable storage operations for AI execution control state.
    /// </summary>
    /// <remarks>
    /// Implementations must be safe for distributed runtime instances. The control state
    /// is used to coordinate pause, resume, cancellation, and human input behavior across
    /// multiple workers.
    /// </remarks>
    public interface IAiExecutionControlStore
    {
        /// <summary>
        /// Attempts to create the control state only if no state already exists for the execution.
        /// </summary>
        /// <param name="state">The initial control state to create.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns><c>true</c> if the state was created; otherwise, <c>false</c>.</returns>
        Task<bool> TryCreateAsync(
            AiExecutionControlState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current control state for an execution.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The current control state, or <c>null</c> if no control state exists.</returns>
        Task<AiExecutionControlState?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the control state for an execution.
        /// </summary>
        /// <param name="state">The control state to save.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetAsync(
            AiExecutionControlState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to update the control state only if the stored version matches the expected version.
        /// </summary>
        /// <param name="state">The new control state to persist.</param>
        /// <param name="expectedVersion">The expected current version.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns><c>true</c> if the update was applied; otherwise, <c>false</c>.</returns>
        Task<bool> TryUpdateAsync(
            AiExecutionControlState state,
            long expectedVersion,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the control state for an execution.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns><c>true</c> if a control state was deleted; otherwise, <c>false</c>.</returns>
        Task<bool> DeleteAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}