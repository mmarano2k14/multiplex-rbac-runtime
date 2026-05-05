using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention
{
    /// <summary>
    /// Defines the contract for the retention policy engine.
    /// </summary>
    /// <remarks>
    /// The retention engine evaluates retention policies and coordinates the physical
    /// retention actions required to apply the resulting decision.
    /// </remarks>
    public interface IAiRetentionEngine : IAiPolicyEngine
    {
        /// <summary>
        /// Evaluates retention policies and returns the retention decision to apply.
        /// </summary>
        /// <param name="context">The retention context to evaluate.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The computed retention decision.</returns>
        Task<AiRetentionDecision> DecideAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates retention policies and applies the resulting retention actions.
        /// </summary>
        /// <param name="context">The retention context to evaluate and apply.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The result of applying retention.</returns>
        Task<AiRetentionApplyResult> ApplyAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default);
    }
}