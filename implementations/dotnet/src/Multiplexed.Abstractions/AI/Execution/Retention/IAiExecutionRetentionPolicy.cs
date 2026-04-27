using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Execution.Retention
{
    /// <summary>
    /// Defines a retention policy responsible for analyzing execution state
    /// and deciding which steps should be compacted or evicted.
    ///
    /// RESPONSIBILITY:
    /// - Read current execution state
    /// - Decide retention actions
    /// - Return a retention plan
    ///
    /// IMPORTANT:
    /// - MUST NOT mutate the state
    /// - MUST NOT access external stores
    /// - MUST be deterministic
    ///
    /// This ensures:
    /// - clean separation of concerns
    /// - testability
    /// - predictable behavior
    /// </summary>
    public interface IAiExecutionRetentionPolicy
    {
        /// <summary>
        /// Gets the retention mode handled by this policy.
        /// </summary>
        AiExecutionRetentionMode Mode { get; }

        /// <summary>
        /// Evaluates the execution state and produces a retention plan.
        /// </summary>
        /// <param name="state">Current execution state (hot state).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A retention plan describing actions to perform.</returns>
        ValueTask<AiExecutionRetentionPlan> EvaluateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}