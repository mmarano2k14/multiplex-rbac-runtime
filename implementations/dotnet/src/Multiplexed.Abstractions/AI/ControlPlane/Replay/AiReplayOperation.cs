namespace Multiplexed.Abstractions.AI.ControlPlane.Replay
{
    /// <summary>
    /// Defines high-level replay control-plane operations.
    ///
    /// This enum is adapter-neutral and can be used later by HTTP API,
    /// MCP server, CLI, dashboard, and Kubernetes control-plane adapters.
    /// </summary>
    public enum AiReplayOperation
    {
        /// <summary>
        /// Runs deterministic replay validation for an existing execution.
        /// </summary>
        Replay = 0,

        /// <summary>
        /// Runs audit-only replay without restoring or mutating runtime execution state.
        /// </summary>
        Audit = 1,

        /// <summary>
        /// Restores an execution from a replay snapshot when supported by the replay engine.
        /// </summary>
        Restore = 2,

        /// <summary>
        /// Retrieves or builds the replay report associated with an execution.
        /// </summary>
        GetReport = 3,

        /// <summary>
        /// Retrieves the decision ledger associated with an execution.
        /// </summary>
        GetLedger = 4,

        /// <summary>
        /// Retrieves the trace timeline associated with an execution.
        /// </summary>
        GetTimeline = 5
    }
}