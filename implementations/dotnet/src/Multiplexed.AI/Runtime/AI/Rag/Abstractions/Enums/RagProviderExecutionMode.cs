namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Defines how multiple RAG providers should be executed by a retrieval strategy.
    ///
    /// PURPOSE:
    /// - Controls the orchestration mode used when a retrieval implementation
    ///   coordinates more than one provider.
    /// - Allows retrieval strategies such as <c>MultiProviderRetrieval</c> to
    ///   choose between sequential, parallel, or fallback execution.
    ///
    /// DESIGN:
    /// - This enum describes execution strategy, not provider capability.
    /// - It is intended to remain deterministic and explicit.
    ///
    /// USAGE:
    /// - Used by retrieval implementations that orchestrate multiple providers.
    /// - Can later be extended with more advanced modes if needed.
    /// </summary>
    public enum RagProviderExecutionMode
    {
        /// <summary>
        /// No execution mode has been specified.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Providers are executed one after another in a fixed order.
        ///
        /// This mode is simple, predictable, and easy to debug.
        /// </summary>
        Sequential = 1,

        /// <summary>
        /// Providers are executed concurrently and their results are merged afterward.
        ///
        /// This mode may improve performance but requires deterministic merging.
        /// </summary>
        Parallel = 2,

        /// <summary>
        /// Providers are executed in order until one returns usable data.
        ///
        /// This mode is useful for prioritized lookup or fallback chains.
        /// </summary>
        Fallback = 3
    }
}