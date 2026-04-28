using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;

namespace Multiplexed.AI.Runtime.Retention.Policies
{
    /// <summary>
    /// No-op retention policy.
    ///
    /// PURPOSE:
    /// - Used when retention mode is disabled (None).
    /// - Ensures the runtime always has a valid policy (no null checks).
    ///
    /// BEHAVIOR:
    /// - Does not compact any step.
    /// - Does not evict any step.
    /// - Leaves the execution state unchanged.
    ///
    /// IMPORTANT:
    /// - Pure decision (returns empty plan).
    /// - No side effects.
    /// </summary>
    public sealed class NoopAiExecutionRetentionPolicy : IAiExecutionRetentionPolicy
    {
        /// <inheritdoc />
        public AiExecutionRetentionMode Mode => AiExecutionRetentionMode.None;

        /// <inheritdoc />
        public ValueTask<AiExecutionRetentionPlan> EvaluateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<AiExecutionRetentionPlan>(
                new AiExecutionRetentionPlan());
        }
    }
}