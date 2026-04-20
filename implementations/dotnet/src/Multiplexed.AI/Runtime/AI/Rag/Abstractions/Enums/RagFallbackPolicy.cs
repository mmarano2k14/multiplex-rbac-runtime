namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Defines how a multi-provider retrieval strategy should react when a provider
    /// returns no data or throws an exception.
    ///
    /// PURPOSE:
    /// - Controls fallback behavior independently from execution mode.
    /// - Allows retrieval orchestration to remain explicit and deterministic.
    ///
    /// USAGE:
    /// - Typically used by multi-provider retrieval implementations.
    /// </summary>
    public enum RagFallbackPolicy
    {
        /// <summary>
        /// No fallback policy has been specified.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// No fallback is applied.
        /// All providers are treated uniformly according to the execution mode.
        /// </summary>
        None = 1,

        /// <summary>
        /// Continue to the next provider only when the current provider returns no items.
        /// </summary>
        OnEmpty = 2,

        /// <summary>
        /// Continue to the next provider only when the current provider throws an exception.
        /// </summary>
        OnError = 3,

        /// <summary>
        /// Continue to the next provider when the current provider returns no items
        /// or throws an exception.
        /// </summary>
        OnEmptyOrError = 4
    }
}