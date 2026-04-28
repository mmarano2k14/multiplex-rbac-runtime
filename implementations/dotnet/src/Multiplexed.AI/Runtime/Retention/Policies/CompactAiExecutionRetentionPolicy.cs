using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;

namespace Multiplexed.AI.Runtime.Retention.Policies
{
    /// <summary>
    /// Retention policy responsible for selecting steps to be compacted.
    ///
    /// PURPOSE:
    /// - Reduce memory footprint of AiExecutionState
    /// - Keep steps inline but remove heavy payload (Result.Data)
    ///
    /// BEHAVIOR:
    /// - Selects terminal steps (Completed or Failed)
    /// - Returns them as candidates for compaction
    ///
    /// IMPORTANT:
    /// - Does NOT mutate state
    /// - Does NOT write payload to store
    /// - Only returns a plan
    ///
    /// FINAL APPLICATION (by RetentionService):
    /// - Externalize payload via payload store
    /// - Replace heavy data with lightweight reference
    /// - Keep step in state
    /// </summary>
    public sealed class CompactAiExecutionRetentionPolicy : IAiExecutionRetentionPolicy
    {
        /// <inheritdoc />
        public AiExecutionRetentionMode Mode => AiExecutionRetentionMode.Compact;

        /// <inheritdoc />
        public ValueTask<AiExecutionRetentionPlan> EvaluateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            var stepsToCompact = state.Steps
                .Where(kvp => IsTerminal(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToArray();

            return new ValueTask<AiExecutionRetentionPlan>(
                new AiExecutionRetentionPlan
                {
                    StepsToCompact = stepsToCompact
                });
        }

        private static bool IsTerminal(AiStepState step)
        {
            return step.Status == AiStepExecutionStatus.Completed ||
                   step.Status == AiStepExecutionStatus.Failed;
        }
    }
}