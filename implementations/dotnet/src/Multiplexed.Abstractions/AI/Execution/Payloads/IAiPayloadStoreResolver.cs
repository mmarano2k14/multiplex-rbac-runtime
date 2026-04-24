namespace Multiplexed.Abstractions.AI.Execution.Payloads
{
    /// <summary>
    /// Resolves the active execution payload store.
    ///
    /// PURPOSE:
    /// - Keeps payload storage selection outside execution logic.
    /// - Allows the runtime to switch between in-memory, MongoDB, or MongoDB+Redis cache.
    /// - Prevents policies and resolvers from being coupled to one concrete store.
    ///
    /// IMPORTANT:
    /// - The resolved store is used for externalized payloads.
    /// - Replay-safe configurations should resolve to a durable store such as MongoDB.
    /// </summary>
    public interface IAiPayloadStoreResolver
    {
        /// <summary>
        /// Resolves the configured payload store.
        /// </summary>
        IAiPayloadStore Resolve();
    }
}