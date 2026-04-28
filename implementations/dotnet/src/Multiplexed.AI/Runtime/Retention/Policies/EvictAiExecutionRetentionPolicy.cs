using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;
using Multiplexed.AI.Configuration;

namespace Multiplexed.AI.Runtime.Retention.Policies
{
    /// <summary>
    /// Retention policy responsible for selecting steps to be evicted
    /// from the hot execution state.
    ///
    /// IMPORTANT:
    /// - Uses AiEngineOptions.StateRetention.MaxCompletedStepsInState.
    /// - No hardcoded threshold.
    /// - This policy only selects candidates.
    /// - RetentionService must persist/index before removal.
    ///
    /// SAFETY:
    /// - Never evicts non-terminal steps.
    /// - Never evicts a terminal step still required by a non-terminal child.
    /// </summary>
    public sealed class EvictAiExecutionRetentionPolicy : IAiExecutionRetentionPolicy
    {
        private readonly IOptions<AiEngineOptions> _options;

        public EvictAiExecutionRetentionPolicy(IOptions<AiEngineOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public AiExecutionRetentionMode Mode => AiExecutionRetentionMode.Evict;

        public ValueTask<AiExecutionRetentionPlan> EvaluateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            var maxInlineSteps = _options.Value.StateRetention.MaxCompletedStepsInState;

            if (maxInlineSteps <= 0 || state.Steps.Count <= maxInlineSteps)
            {
                return new ValueTask<AiExecutionRetentionPlan>(
                    new AiExecutionRetentionPlan());
            }

            var overflow = state.Steps.Count - maxInlineSteps;

            // A completed/failed parent may still be required by a child
            // that is Ready, Running, WaitingForRetry or None.
            // Removing that parent from the hot state can make DAG dependency
            // resolution wait forever, causing retention-related execution loops.
            var protectedStepNames = state.Steps.Values
                .Where(step => !IsTerminal(step))
                .SelectMany(step => step.DependsOn ?? Enumerable.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);

            var stepsToEvict = state.Steps
                .Where(kvp => IsTerminal(kvp.Value))
                .Where(kvp => !protectedStepNames.Contains(kvp.Key))
                .OrderBy(kvp => kvp.Value.CompletedAtUtc ?? kvp.Value.UpdatedAtUtc ?? kvp.Value.StartedAtUtc ?? DateTime.MinValue)
                .ThenBy(kvp => kvp.Value.StepName, StringComparer.Ordinal)
                .Take(overflow)
                .Select(kvp => kvp.Key)
                .ToArray();

            return new ValueTask<AiExecutionRetentionPlan>(
                new AiExecutionRetentionPlan
                {
                    StepsToEvict = stepsToEvict
                });
        }

        private static bool IsTerminal(AiStepState step)
        {
            return step.Status == AiStepExecutionStatus.Completed ||
                   step.Status == AiStepExecutionStatus.Failed;
        }
    }
}