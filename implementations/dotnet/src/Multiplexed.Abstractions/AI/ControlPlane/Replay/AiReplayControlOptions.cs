namespace Multiplexed.Abstractions.AI.ControlPlane.Replay
{
    /// <summary>
    /// Defines options for the replay control-plane facade.
    ///
    /// These options only control the high-level control-plane layer.
    /// They do not modify replay engine internals.
    /// </summary>
    public sealed class AiReplayControlOptions
    {
        /// <summary>
        /// Enables deterministic replay validation operations.
        /// </summary>
        public bool EnableReplay { get; init; } = true;

        /// <summary>
        /// Enables audit-only replay operations.
        /// </summary>
        public bool EnableAudit { get; init; } = true;

        /// <summary>
        /// Enables restore operations from replay snapshots.
        /// </summary>
        public bool EnableRestore { get; init; } = true;

        /// <summary>
        /// Enables replay report retrieval through the control-plane facade.
        /// </summary>
        public bool EnableReportAccess { get; init; } = true;

        /// <summary>
        /// Enables decision ledger access through the control-plane facade.
        /// </summary>
        public bool EnableLedgerAccess { get; init; } = true;

        /// <summary>
        /// Enables trace timeline access through the control-plane facade.
        /// </summary>
        public bool EnableTimelineAccess { get; init; } = true;

        /// <summary>
        /// When enabled, expected operational failures should be returned as
        /// structured failed results instead of being thrown to the caller.
        /// </summary>
        public bool ReturnFailureResultInsteadOfThrowing { get; init; } = true;

        /// <summary>
        /// Enables controller-level duration measurement.
        ///
        /// This is useful for future Grafana metrics and control-plane diagnostics.
        /// </summary>
        public bool MeasureDuration { get; init; } = true;
    }
}