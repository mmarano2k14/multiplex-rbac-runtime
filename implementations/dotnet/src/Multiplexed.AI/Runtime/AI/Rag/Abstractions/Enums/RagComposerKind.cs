namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Describes HOW the final context is built.
    /// </summary>
    public enum RagComposerKind
    {
        Unknown = 0,

        /// <summary>
        /// Builds a structured object.
        /// </summary>
        Structured = 1,

        /// <summary>
        /// Guarantees deterministic ordering and merging.
        /// </summary>
        Deterministic = 2
    }
}