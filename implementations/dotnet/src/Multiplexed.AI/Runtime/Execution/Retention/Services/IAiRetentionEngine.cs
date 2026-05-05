using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Defines the contract for the retention policy engine.
    /// </summary>
    /// <remarks>
    /// The retention engine evaluates retention policies and produces retention decisions.
    /// It does not directly compact payloads, persist archived data, or evict hot state.
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
    }
}