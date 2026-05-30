namespace Multiplexed.Abstractions.AI.ControlPlane.Execution
{
    /// <summary>
    /// Defines the high-level execution control-plane facade.
    ///
    /// This abstraction exposes pause, resume, cancel, human input submission,
    /// and durable execution control status operations without coupling external
    /// adapters to runtime engine internals.
    ///
    /// Intended future callers:
    /// - HTTP API
    /// - MCP server
    /// - CLI
    /// - Dashboard
    /// - Kubernetes control-plane pod
    ///
    /// Important:
    /// This abstraction must not execute DAG steps, claim work, modify local queues,
    /// or replace worker/runtime execution logic.
    /// </summary>
    public interface IAiExecutionControlPlane
    {
        /// <summary>
        /// Executes an execution control-plane operation.
        /// </summary>
        /// <param name="request">The execution control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The execution control-plane result.</returns>
        Task<AiExecutionControlPlaneResult> ExecuteAsync(
            AiExecutionControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests that an execution pause cooperatively.
        /// </summary>
        /// <param name="request">The execution control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The execution control-plane result.</returns>
        Task<AiExecutionControlPlaneResult> PauseAsync(
            AiExecutionControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests that a paused, pausing, waiting, or resuming execution continue.
        /// </summary>
        /// <param name="request">The execution control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The execution control-plane result.</returns>
        Task<AiExecutionControlPlaneResult> ResumeAsync(
            AiExecutionControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests cooperative cancellation of an execution.
        /// </summary>
        /// <param name="request">The execution control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The execution control-plane result.</returns>
        Task<AiExecutionControlPlaneResult> CancelAsync(
            AiExecutionControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Submits human or external input for an execution waiting for input.
        /// </summary>
        /// <param name="request">The execution control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The execution control-plane result.</returns>
        Task<AiExecutionControlPlaneResult> SubmitHumanInputAsync(
            AiExecutionControlPlaneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current durable execution control state.
        /// </summary>
        /// <param name="request">The execution control-plane request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The execution control-plane result.</returns>
        Task<AiExecutionControlPlaneResult> GetStatusAsync(
            AiExecutionControlPlaneRequest request,
            CancellationToken cancellationToken = default);
    }
}