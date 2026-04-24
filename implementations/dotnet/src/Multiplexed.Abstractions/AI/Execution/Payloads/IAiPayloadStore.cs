namespace Multiplexed.Abstractions.AI.Execution.Payloads
{
    /// <summary>
    /// Defines a storage abstraction for execution payloads.
    ///
    /// PURPOSE:
    /// - Provides a pluggable mechanism to persist large execution data outside
    ///   of the main execution state (ledger)
    /// - Enables future ledger compaction and external artifact storage
    ///
    /// DESIGN:
    /// - Payloads are stored as serialized strings (typically JSON)
    /// - The store returns a stable key that can later be resolved
    ///
    /// IMPORTANT:
    /// - This interface must remain deterministic and side-effect safe
    /// - It must not introduce non-deterministic behavior into execution
    /// </summary>
    public interface IAiPayloadStore
    {
        /// <summary>
        /// Persists serialized payload content and returns a unique reference key.
        /// </summary>
        Task<string> SaveAsync(
            string content,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads serialized payload content by its reference key.
        /// </summary>
        Task<string?> LoadAsync(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a payload from the store.
        /// </summary>
        Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default);
    }
}