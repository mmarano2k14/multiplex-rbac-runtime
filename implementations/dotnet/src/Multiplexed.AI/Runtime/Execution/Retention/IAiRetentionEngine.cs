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
        /// <remarks>
        /// This method is intended for non-distributed or terminal retention paths where
        /// it is safe to mutate the in-memory execution state and persist the resulting
        /// state snapshot.
        /// </remarks>
        Task<AiRetentionApplyResult> ApplyAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates retention policies and applies the resulting retention actions through
        /// distributed-safe atomic store patches.
        /// </summary>
        /// <param name="context">The retention context to evaluate and apply.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The result of applying atomic retention.</returns>
        /// <remarks>
        /// <para>
        /// This method is intended for active distributed executions where multiple workers
        /// may still be claiming, running, completing, failing, or retrying steps.
        /// </para>
        ///
        /// <para>
        /// Implementations must not remove steps from a local state snapshot and must not
        /// require a full-state save to apply active retention. Instead, physical hot-state
        /// changes must be delegated to the distributed DAG store through atomic patch
        /// operations.
        /// </para>
        /// </remarks>
        Task<AiRetentionApplyResult> ApplyAtomicAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default);
    }
}