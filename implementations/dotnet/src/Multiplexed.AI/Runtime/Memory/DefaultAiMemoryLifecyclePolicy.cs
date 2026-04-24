using Multiplexed.Abstractions.AI.Memory;

namespace Multiplexed.AI.Runtime.Memory
{
    /// <summary>
    /// Default lifecycle policy for consolidated memories.
    ///
    /// PURPOSE:
    /// - Provides deterministic promotion and pruning thresholds
    /// - Keeps active memory bounded without fixed TTLs
    /// - Lets memories survive based on score rather than age alone
    ///
    /// IMPORTANT:
    /// - This policy only affects consolidated memory
    /// - It must not remove execution ledger records or artifacts
    /// </summary>
    public sealed class DefaultAiMemoryLifecyclePolicy : IAiMemoryLifecyclePolicy
    {
        private const double ActiveThreshold = 0.20;
        private const double PromotionThreshold = 0.75;
        private const double PruneThreshold = 0.05;

        public bool ShouldRemainActive(AiConsolidatedMemoryRecord memory)
        {
            ArgumentNullException.ThrowIfNull(memory);

            return memory.CurrentScore >= ActiveThreshold;
        }

        public bool ShouldPromote(AiConsolidatedMemoryRecord memory)
        {
            ArgumentNullException.ThrowIfNull(memory);

            return memory.CurrentScore >= PromotionThreshold &&
                   memory.Confidence >= 0.70;
        }

        public bool ShouldPrune(AiConsolidatedMemoryRecord memory)
        {
            ArgumentNullException.ThrowIfNull(memory);

            return memory.CurrentScore <= PruneThreshold &&
                   memory.AccessCount == 0 &&
                   memory.AgeInSessions > 1;
        }
    }
}