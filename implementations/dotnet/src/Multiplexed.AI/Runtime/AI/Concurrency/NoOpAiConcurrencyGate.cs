using Multiplexed.Abstractions.AI.Concurrency;

namespace Multiplexed.AI.Runtime.AI.Concurrency
{
    /// <summary>
    /// No-op concurrency gate used when distributed throttling is not configured.
    /// </summary>
    public sealed class NoOpAiConcurrencyGate : IAiConcurrencyGate
    {
        /// <inheritdoc />
        public Task<AiConcurrencyDecision> TryAcquireAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(definition);

            return Task.FromResult(AiConcurrencyDecision.Allow());
        }

        /// <inheritdoc />
        public Task ReleaseAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(definition);

            return Task.CompletedTask;
        }
    }
}