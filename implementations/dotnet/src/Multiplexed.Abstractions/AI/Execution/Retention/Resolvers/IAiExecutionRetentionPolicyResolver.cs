using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;

namespace Multiplexed.Abstractions.AI.Execution.Retention.Resolvers
{
    /// <summary>
    /// Resolves the appropriate retention policy based on the configured mode.
    ///
    /// PURPOSE:
    /// - Centralize policy selection
    /// - Avoid conditional logic scattered across the runtime
    ///
    /// USAGE:
    /// RetentionService → Resolve(mode) → Policy → Evaluate
    /// </summary>
    public interface IAiExecutionRetentionPolicyResolver
    {
        /// <summary>
        /// Resolves the policy associated with the given retention mode.
        /// </summary>
        /// <param name="mode">The retention mode.</param>
        /// <returns>The corresponding retention policy.</returns>
        IAiExecutionRetentionPolicy Resolve(AiExecutionRetentionMode mode);
    }
}