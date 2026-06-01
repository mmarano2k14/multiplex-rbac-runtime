namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Stores and retrieves execution assistance leases.
    /// </summary>
    public interface IAiExecutionAssistanceStore
    {
        /// <summary>
        /// Registers a newly granted assistance lease.
        /// </summary>
        /// <param name="lease">The assistance lease to register.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        Task RegisterAsync(
            AiExecutionAssistanceLease lease,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an assistance lease by identifier.
        /// </summary>
        /// <param name="leaseId">The assistance lease identifier.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The assistance lease when found; otherwise, null.</returns>
        Task<AiExecutionAssistanceLease?> GetAsync(
            string leaseId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists assistance leases associated with an execution.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="includeTerminal">Whether terminal leases should be included.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The assistance leases for the execution.</returns>
        Task<IReadOnlyCollection<AiExecutionAssistanceLease>> ListByExecutionAsync(
            string executionId,
            bool includeTerminal = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists assistance leases associated with a helper runtime instance.
        /// </summary>
        /// <param name="helperRuntimeInstanceId">The helper runtime instance identifier.</param>
        /// <param name="includeTerminal">Whether terminal leases should be included.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The assistance leases for the helper runtime instance.</returns>
        Task<IReadOnlyCollection<AiExecutionAssistanceLease>> ListByHelperAsync(
            string helperRuntimeInstanceId,
            bool includeTerminal = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the status of an assistance lease.
        /// </summary>
        /// <param name="leaseId">The assistance lease identifier.</param>
        /// <param name="status">The new assistance lease status.</param>
        /// <param name="reason">The optional status transition reason.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        Task UpdateStatusAsync(
            string leaseId,
            AiExecutionAssistanceStatus status,
            string? reason = null,
            CancellationToken cancellationToken = default);
    }
}