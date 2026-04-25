using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Metrics;
using Multiplexed.Abstractions.AI.Execution.Retention;

namespace Multiplexed.AI.Runtime.Execution.Retention
{
    /// <summary>
    /// Default execution state retention policy (graph-aware).
    ///
    /// PURPOSE:
    /// - Limits the number of completed steps retained in <see cref="AiExecutionState"/>.
    /// - Prevents unbounded memory growth for long-running executions.
    /// - Preserves a coherent DAG subgraph for debugging and introspection.
    ///
    /// DESIGN:
    /// - Retains the most recent completed steps (retention window).
    /// - Expands retention to include all dependencies required by those steps.
    /// - Removes only completed steps that are not part of the protected subgraph.
    ///
    /// RETENTION RULES:
    /// - Only steps with <see cref="AiStepExecutionStatus.Completed"/> are eligible for removal.
    /// - A step is protected if:
    ///   - It is within the retained window
    ///   - OR it is a dependency (direct or transitive) of a retained step
    ///
    /// IMPORTANT:
    /// - Does NOT remove:
    ///   - Running steps
    ///   - Ready steps
    ///   - WaitingForRetry steps
    ///   - Failed steps
    ///
    /// OBSERVABILITY:
    /// - Emits retention metrics capturing:
    ///   - total steps before/after retention
    ///   - number of evicted steps
    ///   - number of active and pending steps
    ///
    /// SAFETY:
    /// - Must be applied only on terminal executions.
    /// - Preserves DAG integrity and replay/debug consistency.
    /// </summary>
    public sealed class DefaultAiExecutionStateRetentionPolicy
        : IAiExecutionStateRetentionPolicy
    {
        private readonly AiExecutionStateRetentionOptions _options;
        private readonly IAiExecutionRetentionMetrics? _metrics;

        public DefaultAiExecutionStateRetentionPolicy(
            AiExecutionStateRetentionOptions options,
            IAiExecutionRetentionMetrics? metrics = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _metrics = metrics;
        }

        public void Apply(AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            if (!_options.Enabled)
                return;

            if (state.Steps is null || state.Steps.Count == 0)
                return;

            var totalBefore = state.Steps.Count;

            // -------------------------------------------------------------
            // Completed steps ordered by completion time
            // -------------------------------------------------------------
            var completedSteps = state.Steps
                .Where(kvp => kvp.Value.Status == AiStepExecutionStatus.Completed)
                .OrderBy(kvp => kvp.Value.CompletedAtUtc)
                .ToList();

            if (completedSteps.Count <= _options.MaxCompletedStepsInState)
                return;

            // -------------------------------------------------------------
            // STEP 1: Retention window (latest completed steps)
            // -------------------------------------------------------------
            var retainedWindow = completedSteps
                .Skip(completedSteps.Count - _options.MaxCompletedStepsInState)
                .Select(kvp => kvp.Key)
                .ToHashSet(StringComparer.Ordinal);

            // -------------------------------------------------------------
            // STEP 2: Expand dependencies (graph-aware)
            // -------------------------------------------------------------
            var protectedSteps = new HashSet<string>(retainedWindow, StringComparer.Ordinal);

            var stack = new Stack<string>(retainedWindow);

            while (stack.Count > 0)
            {
                var currentKey = stack.Pop();

                if (!state.Steps.TryGetValue(currentKey, out var stepState))
                    continue;

                if (stepState.DependsOn is null)
                    continue;

                foreach (var dep in stepState.DependsOn)
                {
                    if (protectedSteps.Add(dep))
                    {
                        stack.Push(dep);
                    }
                }
            }

            // -------------------------------------------------------------
            // STEP 3: Compute eviction
            // -------------------------------------------------------------
            var excess = completedSteps.Count - _options.MaxCompletedStepsInState;

            var evicted = 0;

            foreach (var kvp in completedSteps)
            {
                if (evicted >= excess)
                    break;

                // Skip protected steps (retained or dependencies)
                if (protectedSteps.Contains(kvp.Key))
                    continue;

                state.Steps.Remove(kvp.Key);
                evicted++;
            }

            var totalAfter = state.Steps.Count;

            // -------------------------------------------------------------
            // Metrics
            // -------------------------------------------------------------
            var activeSteps = state.Steps.Count(kvp =>
                kvp.Value.Status == AiStepExecutionStatus.Running);

            var pendingSteps = state.Steps.Count(kvp =>
                kvp.Value.Status == AiStepExecutionStatus.Ready ||
                kvp.Value.Status == AiStepExecutionStatus.WaitingForRetry);

            _metrics?.RecordRetention(
                totalBefore,
                totalAfter,
                evicted,
                activeSteps,
                pendingSteps);
        }
    }
}