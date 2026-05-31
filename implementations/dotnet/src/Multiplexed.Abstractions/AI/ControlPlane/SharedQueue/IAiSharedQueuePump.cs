namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue
{
    /// <summary>
    /// Defines a pump that repeatedly dispatches pending shared queue items.
    /// </summary>
    /// <remarks>
    /// The pump executes controlled dispatch cycles.
    ///
    /// It does not own admission.
    /// It does not scale Kubernetes.
    /// It does not execute DAG steps.
    /// It does not run forever by itself.
    ///
    /// A background service, CLI command, API endpoint, MCP server, or runtime instance
    /// heartbeat handler can call this pump.
    /// </remarks>
    public interface IAiSharedQueuePump
    {
        /// <summary>
        /// Executes one shared queue pump cycle.
        /// </summary>
        /// <param name="request">The pump request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The pump cycle result.</returns>
        Task<AiSharedQueuePumpResult> PumpOnceAsync(
            AiSharedQueuePumpRequest request,
            CancellationToken cancellationToken = default);
    }
}