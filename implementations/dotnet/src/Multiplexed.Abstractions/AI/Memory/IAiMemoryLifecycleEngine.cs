namespace Multiplexed.Abstractions.AI.Memory
{
    /// <summary>
    /// Applies lifecycle operations to consolidated memories.
    ///
    /// PURPOSE:
    /// - Updates memory scores over work sessions
    /// - Reinforces memories when they are recalled
    /// - Prunes memories that no longer meet lifecycle thresholds
    ///
    /// IMPORTANT:
    /// - This engine operates only on consolidated memory
    /// - It must never mutate the execution ledger
    /// - It must never be required for deterministic replay
    /// </summary>
    public interface IAiMemoryLifecycleEngine
    {
        Task AgeSessionAsync(
            string scope,
            CancellationToken cancellationToken = default);

        Task ReinforceRecallAsync(
            string memoryId,
            CancellationToken cancellationToken = default);

        Task PruneAsync(
            string scope,
            CancellationToken cancellationToken = default);
    }
}