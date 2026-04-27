using System;
using System.Collections.Generic;
using Multiplexed.Abstractions.AI.Execution.Retention;

namespace Multiplexed.AI.Runtime.Retention
{
    /// <summary>
    /// Default implementation of the retention policy resolver.
    ///
    /// DESIGN:
    /// - Uses dependency injection to discover all registered policies
    /// - Builds a dictionary for fast lookup
    /// - Provides O(1) resolution
    ///
    /// FAIL FAST:
    /// - Throws if a requested mode is not registered
    /// </summary>
    public sealed class DefaultAiExecutionRetentionPolicyResolver
        : IAiExecutionRetentionPolicyResolver
    {
        private readonly IReadOnlyDictionary<AiExecutionRetentionMode, IAiExecutionRetentionPolicy> _policies;

        /// <summary>
        /// Initializes the resolver with available policies.
        /// </summary>
        public DefaultAiExecutionRetentionPolicyResolver(
            IEnumerable<IAiExecutionRetentionPolicy> policies)
        {
            var dict = new Dictionary<AiExecutionRetentionMode, IAiExecutionRetentionPolicy>();

            foreach (var policy in policies)
            {
                dict[policy.Mode] = policy;
            }

            _policies = dict;
        }

        /// <inheritdoc />
        public IAiExecutionRetentionPolicy Resolve(AiExecutionRetentionMode mode)
        {
            if (_policies.TryGetValue(mode, out var policy))
            {
                return policy;
            }

            throw new InvalidOperationException(
                $"No retention policy registered for mode '{mode}'.");
        }
    }
}