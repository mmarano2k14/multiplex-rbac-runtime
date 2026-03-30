using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Provides helper methods for DAG-based step selection within an AI pipeline.
    ///
    /// This selector determines which step instances are ready for execution
    /// based on dependency satisfaction and per-step runtime status.
    ///
    /// DESIGN:
    /// - Deterministic
    /// - Stateless
    /// - Pure (no side effects)
    ///
    /// This class does not execute steps.
    /// It only evaluates execution readiness from resolved topology and mutable step state.
    /// </summary>
    public static class AiPipelineDagStepSelector
    {
        /// <summary>
        /// Selects all steps that are currently ready for execution.
        ///
        /// A step is considered ready when:
        /// - it is not already completed
        /// - it is not currently running
        /// - it has not failed
        /// - all declared dependencies are completed
        ///
        /// Steps are returned using deterministic ordering based on <see cref="ResolvedAiPipelineStep.Order"/>.
        /// </summary>
        /// <param name="pipeline">The resolved pipeline.</param>
        /// <param name="state">The mutable execution state.</param>
        /// <returns>The list of ready step instances.</returns>
        public static IReadOnlyList<ResolvedAiPipelineStep> SelectReadySteps(
            ResolvedAiPipeline pipeline,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);

            var readySteps = new List<ResolvedAiPipelineStep>();

            foreach (var step in pipeline.Steps.OrderBy(x => x.Order))
            {
                var stepState = state.GetOrCreateStep(step.Name);

                if (!stepState.IsSchedulable)
                {
                    continue;
                }

                if (step.DependsOn.Count == 0)
                {
                    readySteps.Add(step);
                    continue;
                }

                var dependenciesSatisfied = true;

                foreach (var dependencyName in step.DependsOn)
                {
                    var dependencyState = state.GetOrCreateStep(dependencyName);

                    if (!dependencyState.IsCompleted)
                    {
                        dependenciesSatisfied = false;
                        break;
                    }
                }

                if (dependenciesSatisfied)
                {
                    readySteps.Add(step);
                }
            }

            return readySteps;
        }

        /// <summary>
        /// Selects the next ready step using deterministic ordering.
        /// Returns null when no step is currently executable.
        /// </summary>
        /// <param name="pipeline">The resolved pipeline.</param>
        /// <param name="state">The mutable execution state.</param>
        /// <returns>The next ready step, or null if none is ready.</returns>
        public static ResolvedAiPipelineStep? SelectNextReadyStep(
            ResolvedAiPipeline pipeline,
            AiExecutionState state)
        {
            return SelectReadySteps(pipeline, state).FirstOrDefault();
        }

        /// <summary>
        /// Determines whether the pipeline has completed successfully.
        ///
        /// A pipeline is considered completed only when all step instances
        /// are in the <see cref="AiStepExecutionStatus.Completed"/> state.
        /// </summary>
        /// <param name="pipeline">The resolved pipeline.</param>
        /// <param name="state">The mutable execution state.</param>
        /// <returns>True if all steps are completed; otherwise false.</returns>
        public static bool IsCompleted(
            ResolvedAiPipeline pipeline,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);

            foreach (var step in pipeline.Steps)
            {
                var stepState = state.GetOrCreateStep(step.Name);

                if (!stepState.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }
    }
}