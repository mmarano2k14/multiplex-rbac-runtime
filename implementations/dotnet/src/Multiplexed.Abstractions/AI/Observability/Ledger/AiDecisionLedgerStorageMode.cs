namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Defines the storage mode used by the decision ledger.
    /// </summary>
    public enum AiDecisionLedgerStorageMode
    {
        /// <summary>
        /// Uses a no-operation ledger implementation.
        /// </summary>
        None,

        /// <summary>
        /// Uses an in-memory ledger implementation.
        /// </summary>
        InMemory,

        /// <summary>
        /// Uses a MongoDB-backed durable ledger implementation.
        /// </summary>
        Mongo
    }
}