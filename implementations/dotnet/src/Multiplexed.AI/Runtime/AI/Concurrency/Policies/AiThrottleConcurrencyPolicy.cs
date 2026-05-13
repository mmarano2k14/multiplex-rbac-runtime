using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Concurrency.Policies
{
    /// <summary>
    /// Represents a generic distributed concurrency throttling policy marker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy supports generic throttle configuration such as:
    /// </para>
    ///
    /// <code>
    /// {
    ///   "name": "concurrency.throttle",
    ///   "config": {
    ///     "scope": "provider",
    ///     "target": "openai",
    ///     "limit": 10
    ///   }
    /// }
    /// </code>
    ///
    /// <para>
    /// The actual throttling limit is resolved from policy configuration into
    /// throttle rules, then applied to the effective concurrency definition before
    /// the Redis concurrency gate attempts distributed admission.
    /// </para>
    ///
    /// <para>
    /// This policy itself does not deny admission. It returns an allowed outcome
    /// so the Redis gate can enforce the distributed throttle.
    /// </para>
    /// </remarks>
    [AiPolicy("concurrency.throttle", Kind = AiPolicyKind.Concurrency)]
    public sealed class AiThrottleConcurrencyPolicy
        : AiPolicyBase<AiConcurrencyPolicyContext>
    {
        /// <inheritdoc />
        public override string Key => "concurrency.throttle";

        /// <inheritdoc />
        public override AiPolicyKind Kind => AiPolicyKind.Concurrency;

        /// <inheritdoc />
        public override Task<AiPolicyResult> ExecuteAsync(
            AiConcurrencyPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            cancellationToken.ThrowIfCancellationRequested();

            AiPolicyResult result = AiPolicyResult.Success(
                new AiConcurrencyPolicyOutcome
                {
                    IsAllowed = true
                });

            return Task.FromResult(result);
        }
    }
}