using Multiplexed.Abstractions.AI.Concurrency;

namespace Multiplexed.AI.Runtime.AI.Concurrency
{
    /// <summary>
    /// Represents the context passed to concurrency policies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The base <see cref="AiConcurrencyContext"/> remains focused on distributed admission identity:
    /// execution, pipeline, step, provider, model, operation, runtime instance, and lease id.
    /// </para>
    ///
    /// <para>
    /// Policy-specific configuration is carried separately here so each configured concurrency policy
    /// can evaluate its own configuration without polluting the distributed concurrency context.
    /// </para>
    /// </remarks>
    public sealed class AiConcurrencyPolicyContext
    {
        /// <summary>
        /// Gets the distributed concurrency context being evaluated.
        /// </summary>
        public required AiConcurrencyContext Concurrency { get; init; }

        /// <summary>
        /// Gets the configuration of the currently evaluated policy.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Config { get; init; } =
            new Dictionary<string, object?>();
    }
}