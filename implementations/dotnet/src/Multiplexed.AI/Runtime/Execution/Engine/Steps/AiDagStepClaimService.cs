using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.Execution.Engine.Core;

namespace Multiplexed.AI.Runtime.Execution.Engine.Distributed
{
    /// <summary>
    /// Coordinates distributed DAG step recovery and concurrency-aware claim acquisition.
    /// </summary>
    public sealed class AiDagStepClaimService
    {
        private static readonly IAiConcurrencyDefinitionResolver ConcurrencyDefinitionResolver =
            new DefaultAiConcurrencyDefinitionResolver();

        private readonly IAiDagExecutionEngineServices _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagStepClaimService"/> class.
        /// </summary>
        /// <param name="services">The DAG execution engine services.</param>
        public AiDagStepClaimService(IAiDagExecutionEngineServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Recovers timed-out steps and attempts to claim one ready DAG step.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="workerId">The worker identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The claimed step, or <c>null</c> when no step is ready.</returns>
        public async Task<AiClaimedStep?> ClaimNextAsync(
            string executionId,
            string workerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

            if (_services.DagStore is null)
            {
                throw new InvalidOperationException("Distributed DAG store is not configured.");
            }

            var recoveredCount = await RecoverTimedOutStepsAsync(
                executionId,
                workerId,
                cancellationToken).ConfigureAwait(false);

            if (recoveredCount > 0)
            {
                _services.Logger.Engine.StepsRecovered(executionId, recoveredCount);

                _services.Logger.Engine.LogInformation(
                    $"[AI DAG] Timed-out steps recovered. ExecutionId='{executionId}', RecoveredCount='{recoveredCount}'.");
            }

            var state = await _services.DagStore.GetStateAsync(
                executionId,
                cancellationToken).ConfigureAwait(false);

            if (state is null || state.Steps.Count == 0)
            {
                return null;
            }

            var readySteps = await _services.DagStore.GetReadyStepsAsync(
                executionId,
                maxSteps: 16,
                cancellationToken).ConfigureAwait(false);

            foreach (var readyStep in readySteps)
            {
                if (!state.Steps.TryGetValue(readyStep.StepName, out var stepState))
                {
                    continue;
                }

                var concurrencyDefinition = ConcurrencyDefinitionResolver.Resolve(stepState);

                var concurrencyContext = new AiConcurrencyContext
                {
                    ExecutionId = executionId,
                    PipelineKey = executionId,
                    StepId = readyStep.StepName,
                    StepKey = readyStep.StepName,
                    RuntimeInstanceId = workerId,
                    LeaseId = $"{executionId}:{readyStep.StepName}:{workerId}"
                };

                var gateDecision = await _services.ConcurrencyGate.TryAcquireAsync(
                    concurrencyContext,
                    concurrencyDefinition,
                    cancellationToken).ConfigureAwait(false);

                if (!gateDecision.Allowed)
                {
                    continue;
                }

                var claimed = await _services.ObservabilityService.Tracer.TraceStorageAsync(
                    new AiStorageTraceContext
                    {
                        ExecutionId = executionId,
                        Backend = "Redis",
                        Operation = "TryClaimStep"
                    },
                    async trace =>
                    {
                        var result = await _services.DagStore.TryClaimStepAsync(
                            executionId,
                            readyStep.StepName,
                            workerId,
                            cancellationToken).ConfigureAwait(false);

                        trace.SetTag("claimAcquired", result is not null);
                        trace.SetTag("workerId", workerId);
                        trace.SetTag("stepId", readyStep.StepName);

                        if (result is not null)
                        {
                            trace.SetTag("claimToken", result.ClaimToken);
                        }

                        return result;
                    }).ConfigureAwait(false);

                if (claimed is null)
                {
                    await _services.ConcurrencyGate.ReleaseAsync(
                        concurrencyContext,
                        concurrencyDefinition,
                        cancellationToken).ConfigureAwait(false);

                    continue;
                }

                _services.Logger.Engine.StepClaimed(
                    executionId,
                    claimed.StepName,
                    workerId,
                    claimed.ClaimToken);

                return claimed;
            }

            return null;
        }

        /// <summary>
        /// Recovers timed-out steps and attempts to claim a bounded number of ready DAG steps.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="workerId">The worker identifier.</param>
        /// <param name="maxSteps">The maximum number of steps to claim.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The claimed steps.</returns>
        public async Task<IReadOnlyList<AiClaimedStep>> ClaimBatchAsync(
            string executionId,
            string workerId,
            int maxSteps,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

            if (_services.DagStore is null)
            {
                return Array.Empty<AiClaimedStep>();
            }

            await RecoverTimedOutStepsAsync(
                executionId,
                workerId,
                cancellationToken).ConfigureAwait(false);

            var state = await _services.DagStore.GetStateAsync(
                executionId,
                cancellationToken).ConfigureAwait(false);

            if (state is null || state.Steps.Count == 0)
            {
                return Array.Empty<AiClaimedStep>();
            }

            var readySteps = await _services.DagStore.GetReadyStepsAsync(
                executionId,
                maxSteps,
                cancellationToken).ConfigureAwait(false);

            if (readySteps.Count == 0)
            {
                return Array.Empty<AiClaimedStep>();
            }

            var claimedSteps = new List<AiClaimedStep>(maxSteps);

            foreach (var readyStep in readySteps)
            {
                if (claimedSteps.Count >= maxSteps)
                {
                    break;
                }

                if (!state.Steps.TryGetValue(readyStep.StepName, out var stepState))
                {
                    continue;
                }

                var concurrencyDefinition = ConcurrencyDefinitionResolver.Resolve(stepState);

                var concurrencyContext = new AiConcurrencyContext
                {
                    ExecutionId = executionId,
                    PipelineKey = executionId,
                    StepId = readyStep.StepName,
                    StepKey = readyStep.StepName,
                    RuntimeInstanceId = workerId,
                    LeaseId = $"{executionId}:{readyStep.StepName}:{workerId}"
                };

                var gateDecision = await _services.ConcurrencyGate.TryAcquireAsync(
                    concurrencyContext,
                    concurrencyDefinition,
                    cancellationToken).ConfigureAwait(false);

                if (!gateDecision.Allowed)
                {
                    continue;
                }

                var claimed = await _services.ObservabilityService.Tracer.TraceStorageAsync(
                    new AiStorageTraceContext
                    {
                        ExecutionId = executionId,
                        Backend = "Redis",
                        Operation = "TryClaimStep"
                    },
                    async trace =>
                    {
                        var result = await _services.DagStore.TryClaimStepAsync(
                            executionId,
                            readyStep.StepName,
                            workerId,
                            cancellationToken).ConfigureAwait(false);

                        trace.SetTag("claimAcquired", result is not null);
                        trace.SetTag("workerId", workerId);
                        trace.SetTag("stepId", readyStep.StepName);

                        if (result is not null)
                        {
                            trace.SetTag("claimToken", result.ClaimToken);
                        }

                        return result;
                    }).ConfigureAwait(false);

                if (claimed is null)
                {
                    await _services.ConcurrencyGate.ReleaseAsync(
                        concurrencyContext,
                        concurrencyDefinition,
                        cancellationToken).ConfigureAwait(false);

                    continue;
                }

                _services.Logger.Engine.StepClaimed(
                    executionId,
                    claimed.StepName,
                    workerId,
                    claimed.ClaimToken);

                claimedSteps.Add(claimed);
            }

            return claimedSteps;
        }

        /// <summary>
        /// Recovers timed-out running DAG steps through the distributed store.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="workerId">The worker identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of recovered steps.</returns>
        private async Task<int> RecoverTimedOutStepsAsync(
            string executionId,
            string workerId,
            CancellationToken cancellationToken)
        {
            return await _services.ObservabilityService.Tracer.TraceStorageAsync(
                new AiStorageTraceContext
                {
                    ExecutionId = executionId,
                    Backend = "Redis",
                    Operation = "RecoverTimedOutSteps"
                },
                async trace =>
                {
                    var result = await _services.DagStore!.RecoverTimedOutStepsAsync(
                        executionId,
                        cancellationToken).ConfigureAwait(false);

                    trace.SetTag("recoveredCount", result);
                    trace.SetTag("workerId", workerId);
                    trace.SetTag("recovered", result > 0);

                    return result;
                }).ConfigureAwait(false);
        }
    }
}