namespace Multiplexed.Abstractions.AI.Memory
{
    /// <summary>
    /// Computes memory scores from deterministic memory metadata.
    ///
    /// PURPOSE:
    /// - Assigns and updates usefulness scores for consolidated memories
    /// - Keeps scoring independent from LLM calls
    /// - Enables deterministic ranking, decay, and promotion decisions
    ///
    /// IMPORTANT:
    /// - This policy must not mutate the execution ledger
    /// - This policy must not be required for deterministic replay
    /// </summary>
    public interface IAiMemoryScoringPolicy
    {
        /// <summary>
        /// Computes the initial score for a newly consolidated memory.
        /// </summary>
        double ComputeInitialScore(AiConsolidatedMemoryRecord memory);

        /// <summary>
        /// Computes the current score after recall, session aging, or metadata updates.
        /// </summary>
        double ComputeCurrentScore(AiConsolidatedMemoryRecord memory);
    }
}