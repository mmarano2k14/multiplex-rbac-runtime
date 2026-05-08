namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Defines a runtime concurrency engine responsible for deciding whether
    /// a step may proceed to distributed concurrency-slot acquisition.
    /// </summary>
    public interface IAiConcurrencyEngine
    {
        /// <summary>
        /// Computes a concurrency decision for the specified runtime context.
        /// </summary>
        /// <param name="context">The concurrency evaluation context.</param>
        /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
        /// <returns>The computed concurrency decision.</returns>
        Task<AiConcurrencyDecision> DecideAsync(
            AiConcurrencyContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates concurrency rules for the specified runtime context.
        /// </summary>
        /// <param name="context">The concurrency evaluation context.</param>
        /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
        /// <returns>The concurrency evaluation result.</returns>
        Task<AiConcurrencyEvaluation> EvaluateAsync(
            AiConcurrencyContext context,
            CancellationToken cancellationToken = default);
    }
}