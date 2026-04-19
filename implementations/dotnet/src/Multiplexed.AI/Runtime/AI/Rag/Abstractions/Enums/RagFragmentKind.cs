namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Logical type of a fragment inside the composed context.
    /// </summary>
    public enum RagFragmentKind
    {
        Unknown = 0,

        Entity = 1,
        Fact = 2,
        Signal = 3,
        Runtime = 4,
        Metadata = 5,
        Summary = 6
    }
}