namespace Multiplexed.Abstractions.AI.ControlPlane.Replay
{
    /// <summary>
    /// Defines the high-level replay control-plane facade.
    ///
    /// This abstraction wraps the existing replay engine and exposes replay,
    /// audit, restore, report, ledger, and timeline operations without coupling
    /// external adapters to replay internals.
    ///
    /// Intended future callers:
    /// - HTTP API
    /// - MCP server
    /// - CLI
    /// - Dashboard
    /// - Kubernetes control-plane pod
    ///
    /// Important:
    /// This abstraction must not re-run LLM calls, tool calls, providers,
    /// workflow steps, or DAG execution logic.
    /// </summary>
    public interface IAiReplayControlPlane
    {
        /// <summary>
        /// Executes a replay control-plane operation.
        /// </summary>
        /// <param name="request">Replay control-plane request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Replay control-plane result.</returns>
        Task<AiReplayControlResult> ExecuteAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs deterministic replay validation for an existing execution.
        /// </summary>
        Task<AiReplayControlResult> ReplayAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs audit-only replay for an existing execution.
        /// </summary>
        Task<AiReplayControlResult> AuditAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores an execution from a replay snapshot when supported.
        /// </summary>
        Task<AiReplayControlResult> RestoreAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves or builds the replay report for an existing execution.
        /// </summary>
        Task<AiReplayControlResult> GetReportAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves decision ledger entries associated with an execution.
        /// </summary>
        Task<AiReplayControlResult> GetLedgerAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves trace timeline events associated with an execution.
        /// </summary>
        Task<AiReplayControlResult> GetTimelineAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default);
    }
}