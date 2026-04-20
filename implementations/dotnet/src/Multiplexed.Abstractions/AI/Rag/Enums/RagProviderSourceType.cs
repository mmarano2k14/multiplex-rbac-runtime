namespace Multiplexed.Abstractions.AI.Rag.Enums
{
    /// <summary>
    /// Describes the concrete implementation / source.
    /// </summary>
    public enum RagProviderSourceType
    {
        Unknown = 0,

        Redis = 1,
        Postgres = 2,
        Mongo = 3,
        SqlServer = 4,

        /// <summary>
        /// Internal runtime state (my engine).
        /// </summary>
        RuntimeState = 5,

        Custom = 1000
    }
}