namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Defines configuration options for the decision ledger recorder.
    /// </summary>
    public sealed class AiDecisionLedgerRecorderOptions
    {
        /// <summary>
        /// Gets or sets the write mode used by the decision ledger recorder.
        /// </summary>
        public AiDecisionLedgerWriteMode WriteMode { get; set; } = AiDecisionLedgerWriteMode.BestEffort;
    }
}