using Multiplexed.Abstractions.AI.Execution.Payloads.Models;

namespace Multiplexed.Abstractions.AI.Execution.Payloads.Stores
{
    /// <summary>
    /// Defines a storage abstraction for execution payloads.
    ///
    /// PURPOSE:
    /// - Provides a pluggable mechanism to persist large execution data outside
    ///   of the main execution state (ledger).
    /// - Enables ledger compaction, step eviction, replay, and external artifact storage.
    ///
    /// DESIGN:
    /// - Payloads are stored as serialized strings, typically JSON.
    /// - The store returns a stable key that can later be resolved.
    ///
    /// IMPORTANT:
    /// - This interface must remain deterministic from the runtime point of view.
    /// - It must not introduce non-deterministic execution behavior.
    /// - Existing implementations remain compatible through the basic SaveAsync overload.
    /// </summary>
    public interface IAiPayloadStore
    {
        /// <summary>
        /// Persists serialized payload content and returns a unique reference key.
        ///
        /// This overload is kept for backward compatibility.
        /// </summary>
        Task<string> SaveAsync(
            string content,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists serialized payload content with optional payload metadata
        /// and returns a unique reference key.
        ///
        /// PURPOSE:
        /// - Allows higher-level stores, such as step payload stores,
        ///   to provide semantic metadata without breaking existing stores.
        ///
        /// DEFAULT:
        /// - Delegates to the basic SaveAsync overload.
        /// - Existing stores do not need to change unless they want to persist metadata.
        /// </summary>
        Task<string> SaveAsync(
            string content,
            AiPayloadMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            return SaveAsync(content, cancellationToken);
        }

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