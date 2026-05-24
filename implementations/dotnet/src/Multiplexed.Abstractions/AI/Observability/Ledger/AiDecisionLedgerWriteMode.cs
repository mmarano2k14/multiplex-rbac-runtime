namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Defines how the runtime should behave when writing decision ledger entries.
    /// </summary>
    public enum AiDecisionLedgerWriteMode
    {
        /// <summary>
        /// Decision ledger recording is disabled.
        /// </summary>
        Disabled,

        /// <summary>
        /// Decision ledger recording is best effort.
        /// Ledger failures should be logged but should not break runtime execution.
        /// </summary>
        BestEffort,

        /// <summary>
        /// Decision ledger recording is strict.
        /// Ledger failures should propagate and may fail or block runtime execution.
        /// </summary>
        Strict
    }
}