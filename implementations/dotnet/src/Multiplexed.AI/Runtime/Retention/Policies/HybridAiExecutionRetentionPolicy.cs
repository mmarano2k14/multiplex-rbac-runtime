using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.AI.Configuration;

namespace Multiplexed.AI.Runtime.Retention.Policies
{
    /// <summary>
    /// Retention policy combining compaction and eviction.
    ///
    /// BEHAVIOR:
    /// - If state is under threshold: compact terminal steps only.
    /// - If state is over threshold: evict oldest safe terminal steps,
    ///   compact remaining terminal steps.
    ///
    /// IMPORTANT:
    /// - A step must never be both compacted and evicted in the same plan.
    /// - RetentionService remains responsible for persisting/indexing before removal.
    ///
    /// SAFETY:
    /// - Never evicts non-terminal steps.
    /// - Never evicts a terminal step still required by a non-terminal child.
    /// </summary>
    public sealed class HybridAiExecutionRetentionPolicy : IAiExecutionRetentionPolicy
    {
        private readonly IOptions<AiEngineOptions> _options;

        public HybridAiExecutionRetentionPolicy(IOptions<AiEngineOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public AiExecutionRetentionMode Mode => AiExecutionRetentionMode.Hybrid;

        public ValueTask<AiExecutionRetentionPlan> EvaluateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            var maxInlineSteps = _options.Value.StateRetention.MaxCompletedStepsInState;

            if (maxInlineSteps <= 0)
            {
                return new ValueTask<AiExecutionRetentionPlan>(
                    new AiExecutionRetentionPlan());
            }

            // A completed/failed parent may still be required by a child
            // that is Ready, Running, WaitingForRetry or None.
            // Such steps must stay in the hot state until their dependents
            // reach a terminal status.
            var protectedStepNames = state.Steps.Values
                .Where(step => !IsTerminal(step))
                .SelectMany(step => step.DependsOn ?? Enumerable.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);

            var terminalSteps = state.Steps
                .Where(kvp => IsTerminal(kvp.Value))
                .OrderBy(kvp => kvp.Value.CompletedAtUtc ?? kvp.Value.UpdatedAtUtc ?? kvp.Value.StartedAtUtc ?? DateTime.MinValue)
                .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToArray();

            if (state.Steps.Count <= maxInlineSteps)
            {
                return new ValueTask<AiExecutionRetentionPlan>(
                    new AiExecutionRetentionPlan
                    {
                        StepsToCompact = terminalSteps
                            .Select(kvp => kvp.Key)
                            .ToArray()
                    });
            }

            var overflow = state.Steps.Count - maxInlineSteps;

            var stepsToEvict = terminalSteps
                .Where(kvp => !protectedStepNames.Contains(kvp.Key))
                .Take(overflow)
                .Select(kvp => kvp.Key)
                .ToHashSet(StringComparer.Ordinal);

            var stepsToCompact = terminalSteps
                .Where(kvp => !stepsToEvict.Contains(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToArray();

            return new ValueTask<AiExecutionRetentionPlan>(
                new AiExecutionRetentionPlan
                {
                    StepsToEvict = stepsToEvict.ToArray(),
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