namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents a worker capable of assisting an existing execution
    /// owned by another runtime instance.
    /// </summary>
    public interface IAiExecutionAssistanceWorker
    {
        /// <summary>
        /// Executes assistance work under a granted assistance lease.
        /// </summary>
        /// <param name="lease">The assistance lease authorizing the helper to work on the execution.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        Task AssistAsync(
            AiExecutionAssistanceLease lease,
            CancellationToken cancellationToken = default);
    }
}