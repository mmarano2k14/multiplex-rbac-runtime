using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Base class for strongly-typed AI policies.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    public abstract class AiPolicyBase<TContext> : IAiPolicy<TContext>
    {
        public abstract string Key { get; }
        public abstract AiPolicyKind Kind { get; }

        public async Task<AiPolicyResult> ExecuteAsync(
            object context,
            CancellationToken cancellationToken = default)
        {
            if (context is not TContext typedContext)
            {
                throw new InvalidOperationException(
                    $"Invalid context type. Expected {typeof(TContext).Name}, got {context?.GetType().Name ?? "null"}.");
            }

            return await ExecuteAsync(typedContext, cancellationToken)
                .ConfigureAwait(false);
        }

        public abstract Task<AiPolicyResult> ExecuteAsync(
            TContext context,
            CancellationToken cancellationToken = default);
    }
}