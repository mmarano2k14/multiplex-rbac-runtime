namespace Multiplexed.AI.Observability.Ledger
{
    /// <summary>
    /// Defines MongoDB storage options for the AI decision ledger.
    /// </summary>
    public sealed class MongoAiDecisionLedgerOptions
    {
        /// <summary>
        /// Gets or sets the MongoDB database name used by the decision ledger.
        /// </summary>
        public string DatabaseName { get; set; } = "multiplexed_ai";

        /// <summary>
        /// Gets or sets the MongoDB collection name used by the decision ledger entries.
        /// </summary>
        public string CollectionName { get; set; } = "ai_decision_ledger_entries";

        /// <summary>
        /// Gets or sets the MongoDB collection name used by the decision ledger sequence counters.
        /// </summary>
        public string SequenceCollectionName { get; set; } = "ai_decision_ledger_sequences";

        /// <summary>
        /// Gets or sets a value indicating whether MongoDB indexes should be created by the ledger.
        /// </summary>
        public bool CreateIndexes { get; set; } = true;
    }
}