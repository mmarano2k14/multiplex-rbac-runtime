using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Default structural validator for replay dependency graph consistency.
    /// </summary>
    public sealed class DefaultAiExecutionReplayDependencyGraphValidator
        : IAiExecutionReplayDependencyGraphValidator
    {
        /// <inheritdoc />
        public Task<AiExecutionReplayDependencyGraphValidationResult> ValidateAsync(
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            var issues = new List<AiExecutionReplayIssue>();

            foreach (var step in state.Steps.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                ValidateDependencies(
                    step.Key,
                    step.Value,
                    state,
                    issues);
            }

            return Task.FromResult(new AiExecutionReplayDependencyGraphValidationResult
            {
                IsValid = issues.Count == 0,
                Issues = issues
            });
        }

        /// <summary>
        /// Validates dependencies for a single step.
        /// </summary>
        private static void ValidateDependencies(
            string stepKey,
            AiStepState step,
            AiExecutionState state,
            ICollection<AiExecutionReplayIssue> issues)
        {
            if (step.DependsOn is null || step.DependsOn.Count == 0)
            {
                return;
            }

            foreach (var dependency in step.DependsOn.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    issues.Add(new AiExecutionReplayIssue
                    {
                        Code = "replay.dependency.empty",
                        StepKey = stepKey,
                        Message = "Step contains an empty dependency reference."
                    });

                    continue;
                }

                if (!state.Steps.ContainsKey(dependency))
                {
                    issues.Add(new AiExecutionReplayIssue
                    {
                        Code = "replay.dependency.missing",
                        StepKey = stepKey,
                        Message = $"Step dependency '{dependency}' was not found in replay state."
                    });

                    continue;
                }

                var dependencyStep = state.Steps[dependency];

                if (step.Status is AiStepExecutionStatus.Completed or AiStepExecutionStatus.Running &&
                    dependencyStep.Status != AiStepExecutionStatus.Completed)
                {
                    issues.Add(new AiExecutionReplayIssue
                    {
                        Code = "replay.dependency.not_completed",
                        StepKey = stepKey,
                        Message = $"Step depends on '{dependency}', but the dependency is not completed."
                    });
                }
            }
        }
    }
}