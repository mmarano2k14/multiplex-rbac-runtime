using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Provides deterministic DAG step selection for an AI pipeline.
    ///
    /// PURPOSE:
    /// - Determines which pipeline steps are eligible for execution.
    /// - Evaluates dependency completion and step runtime status.
    /// - Handles local retry readiness checks.
    ///
    /// DESIGN:
    /// - Deterministic: steps are evaluated in resolved pipeline order.
    /// - Stateless selector: no internal state is stored by this class.
    /// - Writer-backed mutation: any missing step state is created through
    ///   <see cref="IAiExecutionStateWriter"/>.
    ///
    /// IMPORTANT:
    /// - This selector does not execute steps.
    /// - This selector is a local readiness helper.
    /// - Distributed multi-worker safety must still be enforced by the claim layer
    ///   such as Redis Lua atomic claim scripts.
    ///
    /// MUTATION:
    /// - Missing step states may be initialized through the state writer.
    /// - WaitingForRetry may be promoted to Ready when retry time is due.
    /// - None may be promoted to Ready when dependencies are satisfied.
    /// </summary>
    public static class AiPipelineDagStepSelector
    {
        /// <summary>
        /// Selects all steps currently eligible for execution.
        ///
        /// The returned list follows deterministic pipeline order.
        /// </summary>
        public static IReadOnlyList<ResolvedAiPipelineStep> SelectReadySteps(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);

            var readySteps = new List<ResolvedAiPipelineStep>();

            foreach (var step in pipeline.Steps.OrderBy(x => x.Order))
            {
                var stepState = stateWriter.GetOrCreateStep(state, step.Name);

                if (!IsStepEligible(
                        stepState,
                        step,
                        state,
                        stateWriter,
                        utcNow))
                {
                    continue;
                }

                readySteps.Add(step);
            }

            return readySteps;
        }

        /// <summary>
        /// Selects the next eligible step using deterministic pipeline order.
        /// </summary>
        public static ResolvedAiPipelineStep? SelectNextReadyStep(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            DateTime utcNow)
        {
            return SelectReadySteps(
                    pipeline,
                    state,
                    stateWriter,
                    utcNow)
                .FirstOrDefault();
        }

        /// <summary>
        /// Determines whether all resolved pipeline steps completed successfully.
        ///
        /// NOTE:
        /// - Missing step states are created through the writer to keep state access centralized.
        /// - Completion is derived from durable step state, not from the global record.
        /// </summary>
        public static bool IsCompleted(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);

            foreach (var step in pipeline.Steps)
            {
                var stepState = stateWriter.GetOrCreateStep(state, step.Name);

                if (!stepState.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a step is currently eligible for execution.
        ///
        /// ELIGIBILITY RULES:
        /// - Completed steps are never eligible.
        /// - Failed steps are never eligible.
        /// - Running steps are never eligible.
        /// - WaitingForRetry steps are eligible only when their retry window is due.
        /// - None steps are promoted to Ready when dependencies are satisfied.
        /// - All declared dependencies must already be completed.
        ///
        /// MUTATION:
        /// - Retry-ready steps may be promoted from WaitingForRetry to Ready.
        /// - Newly schedulable steps may be promoted from None to Ready.
        /// </summary>
        private static bool IsStepEligible(
            AiStepState stepState,
            ResolvedAiPipelineStep step,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(stepState);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);

            if (stepState.Status == AiStepExecutionStatus.Completed ||
                stepState.Status == AiStepExecutionStatus.Failed)
            {
                return false;
            }

            if (stepState.Status == AiStepExecutionStatus.Running)
            {
                return false;
            }

            if (stepState.Status == AiStepExecutionStatus.WaitingForRetry)
            {
                if (stepState.NextRetryAtUtc.HasValue &&
                    stepState.NextRetryAtUtc.Value > utcNow)
                {
                    return false;
                }

                stepState.PromoteRetryToReadyIfDue(utcNow);
            }

            if (step.DependsOn.Count > 0)
            {
                foreach (var dependencyName in step.DependsOn)
                {
                    var dependencyState = stateWriter.GetOrCreateStep(
                        state,
                        dependencyName);

                    if (!dependencyState.IsCompleted)
                    {
                        return false;
                    }
                }
            }

            if (stepState.Status == AiStepExecutionStatus.None)
            {
                stepState.Status = AiStepExecutionStatus.Ready;
                stepState.UpdatedAtUtc = utcNow;
                state.UpdatedAtUtc = utcNow;
            }

            return stepState.IsSchedulable;
        }
    }
}