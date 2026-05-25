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
    /// - Keep hot execution payload memory bounded by selecting old terminal steps for eviction.
    ///
    /// BEHAVIOR:
    /// - Evaluates the execution state from <see cref="AiRetentionContext"/>.
    /// - Computes the number of steps above the configured hot-state threshold.
    /// - Selects a bounded number of the oldest terminal steps per retention pass.
    /// - Skips steps that have already been evicted from hot payload state.
    /// - Produces a <see cref="AiRetentionDecision"/> containing the steps to evict.
    ///
    /// DESIGN:
    /// - This policy is decision-only.
    /// - It does not compact payloads.
    /// - It does not persist archived payloads.
    /// - It does not remove steps from hot state.
    /// - Physical or logical eviction is delegated to <c>IAiRetentionEvictionService</c>
    ///   or to an atomic retention patch service.
    ///
    /// IMPORTANT:
    /// - Safe to execute multiple times.
    /// - Deterministic: same input state produces the same eviction candidates.
    /// - Bounded: limits the number of eviction candidates per pass to avoid aggressive retention loops.
    /// - Idempotent: already logically evicted steps are not selected again.
    /// - The eviction service must persist and index the step payload before removal or logical eviction.
    /// - The distributed DAG store remains the final safety gate and may skip unsafe candidates.
    /// - During active execution, eviction should preserve the step shell and only remove heavy payload data.
    /// </remarks>
    [AiPolicy("retention.evict.terminal", Kind = AiPolicyKind.Retention)]
    public sealed class EvictAiRetentionPolicy : AiPolicyBase<AiRetentionContext>
    {
        private const int MaxEvictionsPerPass = 10;

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

            var maxStepsInState = context.Trigger?.MaxStepsInState ?? 0;

            if (maxStepsInState <= 0)
            {
                return Task.FromResult<AiPolicyResult>(
                    AiPolicyResult.Success(
                        AiRetentionDecision.None("MaxStepsInState is not configured.")));
            }

            var hotPayloadStepsCount = context.ExecutionState.Steps
                .Count(step => !step.Value.IsEvictedFromHotState);

            var overflow = hotPayloadStepsCount - maxStepsInState;

            if (overflow <= 0)
            {
                return Task.FromResult<AiPolicyResult>(
                    AiPolicyResult.Success(
                        AiRetentionDecision.None("Hot payload state is within the configured limit.")));
            }

            var evictionCount = Math.Min(
                overflow,
                MaxEvictionsPerPass);

            var stepsToEvict = context.ExecutionState.Steps
                .Where(step =>
                    IsTerminal(step.Value) &&
                    !step.Value.IsEvictedFromHotState)
                .OrderBy(step => ResolveTerminalTimestamp(step.Value))
                .ThenBy(step => step.Key, StringComparer.Ordinal)
                .Take(evictionCount)
                .Select(step => step.Key)
                .ToArray();

            if (stepsToEvict.Length == 0)
            {
                return Task.FromResult<AiPolicyResult>(
                    AiPolicyResult.Success(
                        AiRetentionDecision.None("No non-evicted terminal steps were available for eviction.")));
            }

            var result = AiPolicyResult.Success(
                new AiRetentionDecision
                {
                    Kind = AiRetentionDecisionKind.Evict,
                    StepsToEvict = stepsToEvict,
                    Reason = $"Selected {stepsToEvict.Length} oldest terminal step(s) for bounded hot-payload eviction."
                });

            return Task.FromResult<AiPolicyResult>(result);
        }

        /// <summary>
        /// Determines whether the specified step is in a terminal state.
        /// </summary>
        /// <param name="step">The step state to evaluate.</param>
        /// <returns><c>true</c> if the step is completed or failed; otherwise, <c>false</c>.</returns>
        private static bool IsTerminal(
            AiStepState step)
        {
            return step.Status == AiStepExecutionStatus.Completed ||
                   step.Status == AiStepExecutionStatus.Failed;
        }

        /// <summary>
        /// Resolves the timestamp used to order terminal steps for eviction.
        /// </summary>
        /// <param name="step">The step state to evaluate.</param>
        /// <returns>The best available terminal timestamp, or <see cref="DateTime.MaxValue"/>.</returns>
        private static DateTime ResolveTerminalTimestamp(
            AiStepState step)
        {
            return step.CompletedAtUtc ?? DateTime.MaxValue;
        }
    }
}