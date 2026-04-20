namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Defines how retrieval results coming from multiple providers should be merged.
    ///
    /// PURPOSE:
    /// - Makes merge semantics explicit.
    /// - Keeps orchestration behavior configurable and testable.
    /// </summary>
    public enum RagMergeMode
    {
        /// <summary>
        /// No merge mode has been specified.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Concatenate all results in deterministic order.
        /// </summary>
        Concat = 1,

        /// <summary>
        /// Merge results using deterministic union semantics.
        /// </summary>
        StableUnion = 2,

        /// <summary>
        /// Keep all results but prefer the highest scored entry when duplicates are detected.
        /// </summary>
        BestScoreWins = 3
    }
}