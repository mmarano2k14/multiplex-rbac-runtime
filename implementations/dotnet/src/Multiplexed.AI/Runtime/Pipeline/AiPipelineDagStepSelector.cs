using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Provides deterministic DAG step selection for an AI pipeline.
    /// </summary>
    public static class AiPipelineDagStepSelector
    {
        /// <summary>
        /// Selects all steps currently eligible for execution using hot state only.
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

            foreach (var step in state.Steps.Values)
            {
                step.PromoteRetryToReadyIfDue(DateTime.Now);
            }

            var readySteps = new List<ResolvedAiPipelineStep>();

            foreach (var step in pipeline.Steps.OrderBy(x => x.Order))
            {
                var stepState = stateWriter.GetOrCreateStep(state, step.Name);

                if (!IsStepEligible(stepState, step, state, stateWriter, utcNow))
                {
                    continue;
                }

                readySteps.Add(step);
            }

            return readySteps;
        }

        /// <summary>
        /// Selects all steps currently eligible for execution using hot state plus archived step resolution.
        ///
        /// PURPOSE:
        /// - Supports step eviction through the external step payload index.
        /// - Allows dependency checks to resolve evicted completed steps.
        /// - Avoids full archived step payload loading when only dependency status is needed.
        /// - Preserves the same retry and readiness mutation rules as the hot-state selector.
        /// </summary>
        public static async Task<IReadOnlyList<ResolvedAiPipelineStep>> SelectReadyStepsAsync(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            IAiExecutionStepResolver resolver,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);
            ArgumentNullException.ThrowIfNull(resolver);

            var readySteps = new List<ResolvedAiPipelineStep>();

            foreach (var step in pipeline.Steps.OrderBy(x => x.Order))
            {
                var stepState = stateWriter.GetOrCreateStep(state, step.Name);

                if (!await IsStepEligibleAsync(
                        stepState,
                        step,
                        state,
                        stateWriter,
                        resolver,
                        utcNow,
                        cancellationToken).ConfigureAwait(false))
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
        /// Selects the next eligible step using deterministic pipeline order and archived step resolution.
        /// </summary>
        public static async Task<ResolvedAiPipelineStep?> SelectNextReadyStepAsync(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            IAiExecutionStepResolver resolver,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var readySteps = await SelectReadyStepsAsync(
                    pipeline,
                    state,
                    stateWriter,
                    resolver,
                    utcNow,
                    cancellationToken)
                .ConfigureAwait(false);

            return readySteps.FirstOrDefault();
        }

        /// <summary>
        /// Determines whether all resolved pipeline steps completed successfully using hot state only.
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
        /// Determines whether all resolved pipeline steps completed successfully using hot state plus archived step status.
        ///
        /// IMPORTANT:
        /// - Uses lightweight archived status when possible.
        /// - Does not load full archived step payloads just to verify completion.
        /// </summary>
        public static async Task<bool> IsCompletedAsync(
            ResolvedAiPipeline pipeline,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            IAiExecutionStepResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);
            ArgumentNullException.ThrowIfNull(resolver);

            foreach (var step in pipeline.Steps)
            {
                var stepState = await resolver.GetStepStatusAsync(
                        state.ExecutionId,
                        step.Name,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                stepState ??= stateWriter.GetOrCreateStep(state, step.Name);

                if (!stepState.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a step is currently eligible for execution using hot state only.
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
                if (stepState?.RetryState?.NextRetryAtUtc.HasValue ?? false &&
                    stepState.RetryState.NextRetryAtUtc.Value > utcNow)
                {
                    return false;
                }

                stepState!.PromoteRetryToReadyIfDue(utcNow);
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

        /// <summary>
        /// Determines whether a step is currently eligible for execution using hot state plus archived dependency status.
        ///
        /// IMPORTANT:
        /// - Dependencies only require status checks.
        /// - Uses GetStepStatusAsync to avoid loading full archived payloads.
        /// </summary>
        private static async Task<bool> IsStepEligibleAsync(
            AiStepState stepState,
            ResolvedAiPipelineStep step,
            AiExecutionState state,
            IAiExecutionStateWriter stateWriter,
            IAiExecutionStepResolver resolver,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stepState);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stateWriter);
            ArgumentNullException.ThrowIfNull(resolver);

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
                if (stepState?.RetryState?.NextRetryAtUtc.HasValue ?? false &&
                    stepState.RetryState.NextRetryAtUtc.Value > utcNow)
                {
                    return false;
                }

                stepState!.PromoteRetryToReadyIfDue(utcNow);
            }

            if (step.DependsOn.Count > 0)
            {
                foreach (var dependencyName in step.DependsOn)
                {
                    AiStepState? dependencyState;

                    if (!state.Steps.TryGetValue(dependencyName, out dependencyState))
                    {
                        dependencyState = await resolver.GetStepStatusAsync(
                                state.ExecutionId,
                                dependencyName,
                                state,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    dependencyState ??= stateWriter.GetOrCreateStep(
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