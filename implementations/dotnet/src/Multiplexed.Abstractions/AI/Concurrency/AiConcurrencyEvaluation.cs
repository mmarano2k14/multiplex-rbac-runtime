namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Represents the result of a concurrency evaluation.
    /// </summary>
    public sealed class AiConcurrencyEvaluation
    {
        /// <summary>
        /// Gets the resolved concurrency definition.
        /// </summary>
        public required AiConcurrencyDefinition Definition { get; init; }

        /// <summary>
        /// Gets the computed concurrency decision.
        /// </summary>
        public required AiConcurrencyDecision Decision { get; init; }
    }
}