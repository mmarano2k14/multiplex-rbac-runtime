namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Describes the FUNCTIONAL role of a provider.
    /// Not the technology, but what it does.
    /// </summary>
    public enum RagProviderKind
    {
        Unknown = 0,

        Vector = 1,
        Sql = 2,
        Runtime = 3
    }
}