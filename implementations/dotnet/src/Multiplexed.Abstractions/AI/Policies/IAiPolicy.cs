namespace Multiplexed.AI.Abstractions.AI.Policies
{
    /// <summary>
    /// Represents a policy that can be executed against a runtime context.
    /// </summary>
    /// <remarks>
    /// Policies are responsible for evaluating a given context and producing a result
    /// that can be consumed by higher-level engines such as retry, retention, or eviction.
    /// </remarks>
    public interface IAiPolicy
    {
        /// <summary>
        /// Gets the unique key identifying the policy.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the policy kind.
        /// </summary>
        AiPolicyKind Kind { get; }

        /// <summary>
        /// Executes the policy against the specified context.
        /// </summary>
        /// <param name="context">The runtime context.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The policy result.</returns>
        Task<AiPolicyResult> ExecuteAsync(
            object context,
            CancellationToken cancellationToken = default);
    }
}