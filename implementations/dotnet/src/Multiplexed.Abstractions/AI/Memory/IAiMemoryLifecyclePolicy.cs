namespace Multiplexed.Abstractions.AI.Memory
{
    /// <summary>
    /// Defines lifecycle decisions for consolidated memories.
    ///
    /// PURPOSE:
    /// - Determines whether a memory should remain active
    /// - Determines whether a memory should be promoted, retained, or pruned
    /// - Keeps memory lifecycle separate from execution state management
    ///
    /// IMPORTANT:
    /// - Lifecycle decisions apply only to consolidated memory
    /// - They must never delete execution ledger data
    /// </summary>
    public interface IAiMemoryLifecyclePolicy
    {
        bool ShouldRemainActive(AiConsolidatedMemoryRecord memory);

        bool ShouldPromote(AiConsolidatedMemoryRecord memory);

        bool ShouldPrune(AiConsolidatedMemoryRecord memory);
    }
}