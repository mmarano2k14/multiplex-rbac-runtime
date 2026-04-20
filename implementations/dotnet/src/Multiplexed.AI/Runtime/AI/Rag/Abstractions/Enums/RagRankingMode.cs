namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Defines how merged results should be ranked before final stable ordering is assigned.
    ///
    /// PURPOSE:
    /// - Makes ranking behavior explicit.
    /// - Allows consistent deterministic ordering across providers.
    /// </summary>
    public enum RagRankingMode
    {
        /// <summary>
        /// No ranking mode has been specified.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Do not rank by score. Preserve merge order only.
        /// </summary>
        None = 1,

        /// <summary>
        /// Rank by score descending, then apply deterministic tie-breakers.
        /// </summary>
        ScoreDescending = 2,

        /// <summary>
        /// Rank deterministically using score, provider key, source type, and identifier.
        /// </summary>
        DeterministicScoreThenId = 3
    }
}