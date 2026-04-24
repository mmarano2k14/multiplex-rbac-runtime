namespace Multiplexed.Abstractions.AI.Memory
{
    /// <summary>
    /// Stores and retrieves consolidated AI memories.
    ///
    /// PURPOSE:
    /// - Provides durable access to long-term memory records
    /// - Keeps consolidated memory separate from the execution ledger
    /// - Supports retrieval, reinforcement, aging, and pruning
    ///
    /// IMPORTANT:
    /// - This store must not be used as a replay source of truth
    /// - Replay must continue to depend on execution ledger + payload artifacts
    /// </summary>
    public interface IAiConsolidatedMemoryStore
    {
        Task SaveAsync(
            AiConsolidatedMemoryRecord memory,
            CancellationToken cancellationToken = default);

        Task<AiConsolidatedMemoryRecord?> GetAsync(
            string id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AiConsolidatedMemoryRecord>> SearchAsync(
            string scope,
            string? kind = null,
            int limit = 20,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string id,
            CancellationToken cancellationToken = default);
    }
}