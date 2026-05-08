namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Defines a distributed concurrency gate responsible for acquiring and releasing
    /// runtime execution capacity.
    /// </summary>
    public interface IAiConcurrencyGate
    {
        /// <summary>
        /// Attempts to acquire a distributed concurrency slot.
        /// </summary>
        Task<AiConcurrencyDecision> TryAcquireAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases a previously acquired distributed concurrency slot.
        /// </summary>
        Task ReleaseAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            CancellationToken cancellationToken = default);
    }
}