using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Policies
{
    /// <summary>
    /// Retention policy responsible for selecting terminal steps eligible for eviction.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Identify terminal steps that can be safely removed from hot execution state.
    ///
    /// BEHAVIOR:
    /// - Evaluates the execution state from <see cref="AiRetentionContext"/>.
    /// - Selects all terminal steps.
    /// - Produces a <see cref="AiRetentionDecision"/> containing the steps to evict.
    ///
    /// DESIGN:
    /// - This policy is decision-only.
    /// - It does not compact payloads.
    /// - It does not persist archived payloads.
    /// - It does not remove steps from hot state.
    /// - Physical eviction is delegated to <c>IAiRetentionEvictionService</c>.
    ///
    /// IMPORTANT:
    /// - Safe to execute multiple times.
    /// - Deterministic: same input state produces the same eviction candidates.
    /// - The eviction service must persist and index the step payload before removal.
    /// - This policy does not apply inline payload thresholds; those belong to compaction policies.
    /// </remarks>
    [AiPolicy("retention.evict.terminal", Kind = AiPolicyKind.Retention)]
    public sealed class EvictAiRetentionPolicy : AiPolicyBase<AiRetentionContext>
    {
        /// <summary>
        /// Gets the unique key identifying the policy.
        /// </summary>
        public override string Key => "retention.evict.terminal";

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

            var protectedDependencies = context.ExecutionState.Steps.Values
                .Where(step => !IsTerminal(step))
                .SelectMany(step => step.DependsOn ?? Enumerable.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);

            var stepsToEvict = context.ExecutionState.Steps
                .Where(step =>
                    IsTerminal(step.Value) &&
                    !protectedDependencies.Contains(step.Key))
                .Select(step => step.Key)
                .ToArray();

            var result = AiPolicyResult.Success(
                new AiRetentionDecision
                {
                    Kind = AiRetentionDecisionKind.Evict,
                    StepsToEvict = stepsToEvict,
                    Reason = "Terminal steps selected for eviction."
                });

            return Task.FromResult<AiPolicyResult>(result);
        }

        /// <summary>
        /// Determines whether the specified step is in a terminal state.
        /// </summary>
        /// <param name="step">The step state to evaluate.</param>
        /// <returns><c>true</c> if the step is completed or failed; otherwise, <c>false</c>.</returns>
        private static bool IsTerminal(AiStepState step)
        {
            return step.Status == AiStepExecutionStatus.Completed ||
                   step.Status == AiStepExecutionStatus.Failed;
        }
    }
}