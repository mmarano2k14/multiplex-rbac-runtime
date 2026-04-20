namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Defines how duplicate items should be detected across merged provider results.
    ///
    /// PURPOSE:
    /// - Makes deduplication semantics explicit.
    /// - Allows retrieval aggregation to remain deterministic and predictable.
    /// </summary>
    public enum RagDeduplicationMode
    {
        /// <summary>
        /// No deduplication mode has been specified.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// No deduplication is applied.
        /// </summary>
        None = 1,

        /// <summary>
        /// Deduplicate by item identifier only.
        /// </summary>
        ById = 2,

        /// <summary>
        /// Deduplicate by provider key and item identifier.
        /// </summary>
        BySourceAndId = 3
    }
}