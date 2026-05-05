using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Policies
{
    /// <summary>
    /// Retention policy responsible for selecting terminal steps for hybrid retention.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Select steps that are eligible for a hybrid retention flow.
    /// - Hybrid retention combines compaction and eviction decisions.
    ///
    /// BEHAVIOR:
    /// - Evaluates the execution state from <see cref="AiRetentionContext"/>.
    /// - Selects all terminal steps.
    /// - Produces a <see cref="AiRetentionDecision"/> containing both compaction
    ///   and eviction candidates.
    ///
    /// DESIGN:
    /// - This policy is decision-only.
    /// - It does not compact payloads.
    /// - It does not persist archived data.
    /// - It does not remove steps from hot execution state.
    ///
    /// IMPORTANT:
    /// - Safe to execute multiple times.
    /// - Deterministic: same input state produces the same hybrid candidates.
    /// - The retention engine preserves both decisions. If a step is selected for both
    ///   compaction and eviction, it is compacted first, then evicted.
    /// </remarks>
    [AiPolicy("retention.hybrid.terminal", Kind = AiPolicyKind.Retention)]
    public sealed class HybridAiRetentionPolicy : AiPolicyBase<AiRetentionContext>
    {
        /// <summary>
        /// Gets the unique key identifying the policy.
        /// </summary>
        public override string Key => "retention.hybrid.terminal";

        /// <summary>
        /// Gets the policy kind.
        /// </summary>
        public override AiPolicyKind Kind => AiPolicyKind.Retention;

        /// <inheritdoc />
        public override Task<AiPolicyResult> ExecuteAsync(
            AiRetentionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.ExecutionState);

            var terminalSteps = context.ExecutionState.Steps
                .Where(step => IsTerminal(step.Value))
                .Select(step => step.Key)
                .ToArray();

            var result = AiPolicyResult.Success(
                new AiRetentionDecision
                {
                    Kind = AiRetentionDecisionKind.Hybrid,
                    StepsToCompact = terminalSteps,
                    StepsToEvict = terminalSteps,
                    Reason = "Terminal steps selected for hybrid retention."
                });

            return Task.FromResult<AiPolicyResult>(result);
        }

        /// <summary>
        /// Determines whether the specified step is in a terminal state.
        /// </summary>
        /// <param name="step">The step state to evaluate.</param>
        /// <returns>
        /// <c>true</c> if the step is completed or failed; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsTerminal(AiStepState step)
        {
            return step.Status == AiStepExecutionStatus.Completed ||
                   step.Status == AiStepExecutionStatus.Failed;
        }
    }
}