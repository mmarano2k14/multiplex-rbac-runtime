namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Stores active executions that may receive cross-instance assistance.
    /// </summary>
    /// <remarks>
    /// The candidate store allows idle runtime instances to discover active
    /// executions owned by other primary runtime instances. Implementations may be
    /// in-memory for local tests or distributed for multi-node deployments.
    /// </remarks>
    public interface IAiExecutionAssistanceCandidateStore
    {
        /// <summary>
        /// Registers or updates an active assistance candidate.
        /// </summary>
        /// <param name="candidate">The candidate to register or update.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        Task UpsertAsync(
            AiExecutionAssistanceCandidate candidate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an assistance candidate by execution identifier.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The candidate when found; otherwise, null.</returns>
        Task<AiExecutionAssistanceCandidate?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists active assistance candidates.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The active assistance candidates.</returns>
        Task<IReadOnlyCollection<AiExecutionAssistanceCandidate>> ListActiveAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a candidate as completed and removes it from active assistance selection.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="reason">The completion or removal reason.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        Task MarkCompletedAsync(
            string executionId,
            string? reason = null,
            CancellationToken cancellationToken = default);
    }
}