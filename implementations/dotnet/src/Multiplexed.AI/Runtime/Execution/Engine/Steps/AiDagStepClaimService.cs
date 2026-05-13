using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Engine.Steps
{
    /// <summary>
    /// Coordinates distributed DAG step recovery and concurrency-aware claim acquisition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service is responsible for selecting ready DAG step candidates, applying distributed
    /// concurrency admission, and then atomically claiming the admitted step through the distributed
    /// DAG store.
    /// </para>
    ///
    /// <para>
    /// The service intentionally separates three concerns:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item>
    /// <description>Recover timed-out distributed steps.</description>
    /// </item>
    /// <item>
    /// <description>Acquire distributed concurrency capacity through the concurrency gate.</description>
    /// </item>
    /// <item>
    /// <description>Claim step ownership through the DAG store.</description>
    /// </item>
    /// </list>
    ///
    /// <para>
    /// If concurrency capacity is acquired but the DAG claim fails, the lease is released immediately.
    /// This prevents a losing worker from holding distributed capacity for a step it did not actually own.
    /// </para>
    ///
    /// <para>
    /// Throttled steps are traced and logged with the diagnostic reason returned by the concurrency gate.
    /// This makes distributed admission decisions observable in production.
    /// </para>
    /// </remarks>
    public sealed class AiDagStepClaimService
    {
        private static readonly IAiConcurrencyDefinitionResolver ConcurrencyDefinitionResolver =
            new DefaultAiConcurrencyDefinitionResolver();

        private readonly IAiDagExecutionEngineServices _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagStepClaimService"/> class.
        /// </summary>
        /// <param name="services">
        /// The composed DAG execution engine services.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public AiDagStepClaimService(IAiDagExecutionEngineServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Recovers timed-out steps and attempts to claim one ready DAG step.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable logical pipeline key used for distributed pipeline-level throttling.
        /// This value must be stable across multiple executions of the same pipeline.
        /// A recommended format is <c>{PipelineName}:{PipelineVersion}</c>.
        /// </param>
        /// <param name="workerId">
        /// The worker or runtime instance identifier.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The claimed step, or <c>null</c> when no step is ready, no step can be admitted,
        /// or all admitted candidates lose the distributed claim race.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method evaluates ready candidates one by one. Each candidate must first pass
        /// distributed concurrency admission before the service attempts to claim the step.
        /// </para>
        ///
        /// <para>
        /// If a step is throttled, the denial is traced and logged, then the service continues
        /// evaluating the next ready candidate.
        /// </para>
        /// </remarks>
        public async Task<AiClaimedStep?> ClaimNextAsync(
            string executionId,
            string pipelineKey,
            string workerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
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

                var concurrencyContext = AiDagExecutionHelpers.CreateConcurrencyContext(
                     executionId,
                     pipelineKey,
                     readyStep.StepName,
                     workerId,
                     stepState);

                var gateDecision = await TryAcquireConcurrencyLeaseAsync(
                    concurrencyContext,
                    concurrencyDefinition,
                    readyStep.StepName,
                    cancellationToken).ConfigureAwait(false);

                if (!gateDecision.Allowed)
                {
                    LogThrottledStep(
                        executionId,
                        pipelineKey,
                        readyStep.StepName,
                        workerId,
                        gateDecision);

                    continue;
                }

                var claimed = await TryClaimStepAsync(
                    executionId,
                    readyStep.StepName,
                    workerId,
                    cancellationToken).ConfigureAwait(false);

                if (claimed is null)
                {
                    await _services.ConcurrencyGate.ReleaseAsync(
                        concurrencyContext,
                        concurrencyDefinition,
                        cancellationToken).ConfigureAwait(false);

                    _services.Logger.Engine.LogInformation(
                        $"[AI DAG] Concurrency lease released after failed claim. ExecutionId='{executionId}', StepName='{readyStep.StepName}', Worker='{workerId}'.");

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
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable logical pipeline key used for distributed pipeline-level throttling.
        /// This value must be stable across multiple executions of the same pipeline.
        /// A recommended format is <c>{PipelineName}:{PipelineVersion}</c>.
        /// </param>
        /// <param name="workerId">
        /// The worker or runtime instance identifier.
        /// </param>
        /// <param name="maxSteps">
        /// The maximum number of steps to claim.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The claimed steps.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Each ready candidate is independently admitted, claimed, and returned.
        /// A batch therefore owns one concurrency lease per claimed step, not one lease for the
        /// whole batch.
        /// </para>
        ///
        /// <para>
        /// If a candidate is admitted but the actual DAG claim fails, the corresponding lease is
        /// released immediately and the service continues with the next candidate.
        /// </para>
        /// </remarks>
        public async Task<IReadOnlyList<AiClaimedStep>> ClaimBatchAsync(
            string executionId,
            string pipelineKey,
            string workerId,
            int maxSteps,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
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

                var concurrencyContext = AiDagExecutionHelpers.CreateConcurrencyContext(
                     executionId,
                     pipelineKey,
                     readyStep.StepName,
                     workerId,
                     stepState);

                var gateDecision = await TryAcquireConcurrencyLeaseAsync(
                    concurrencyContext,
                    concurrencyDefinition,
                    readyStep.StepName,
                    cancellationToken).ConfigureAwait(false);

                if (!gateDecision.Allowed)
                {
                    LogThrottledStep(
                        executionId,
                        pipelineKey,
                        readyStep.StepName,
                        workerId,
                        gateDecision);

                    continue;
                }

                var claimed = await TryClaimStepAsync(
                    executionId,
                    readyStep.StepName,
                    workerId,
                    cancellationToken).ConfigureAwait(false);

                if (claimed is null)
                {
                    await _services.ConcurrencyGate.ReleaseAsync(
                        concurrencyContext,
                        concurrencyDefinition,
                        cancellationToken).ConfigureAwait(false);

                    _services.Logger.Engine.LogInformation(
                        $"[AI DAG] Concurrency lease released after failed claim. ExecutionId='{executionId}', StepName='{readyStep.StepName}', Worker='{workerId}'.");

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
        /// Attempts to acquire distributed concurrency capacity for a ready step candidate.
        /// </summary>
        /// <param name="context">
        /// The concurrency context for the ready step.
        /// </param>
        /// <param name="definition">
        /// The resolved concurrency definition.
        /// </param>
        /// <param name="stepName">
        /// The ready step name.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The concurrency decision returned by the configured concurrency gate.
        /// </returns>
        /// <remarks>
        /// The acquisition is traced as a storage operation because the default distributed gate is
        /// Redis-backed. The trace includes the step, pipeline, worker, lease id, admission result,
        /// and denial reason when throttled.
        /// </remarks>
        private async Task<AiConcurrencyDecision> TryAcquireConcurrencyLeaseAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            string stepName,
            CancellationToken cancellationToken)
        {
            return await _services.ObservabilityService.Tracer.TraceStorageAsync(
                new AiStorageTraceContext
                {
                    ExecutionId = context.ExecutionId,
                    StepId = stepName,
                    Backend = "Redis",
                    Operation = "TryAcquireConcurrencyLease"
                },
                async trace =>
                {
                    var decision = await _services.ConcurrencyGate.TryAcquireAsync(
                        context,
                        definition,
                        cancellationToken).ConfigureAwait(false);

                    trace.SetTag("concurrency.allowed", decision.Allowed);
                    trace.SetTag("concurrency.pipelineKey", context.PipelineKey);
                    trace.SetTag("concurrency.stepKey", context.StepKey);
                    trace.SetTag("concurrency.leaseId", context.LeaseId);
                    trace.SetTag("workerId", context.RuntimeInstanceId);

                    if (!decision.Allowed)
                    {
                        trace.SetTag("concurrency.denied", true);
                        trace.SetTag("concurrency.reason", decision.Reason ?? "Concurrency limit reached.");
                    }

                    return decision;
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to atomically claim a specific ready step through the distributed DAG store.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="stepName">
        /// The step name to claim.
        /// </param>
        /// <param name="workerId">
        /// The worker identifier.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The claimed step when claim acquisition succeeds; otherwise, <c>null</c>.
        /// </returns>
        private async Task<AiClaimedStep?> TryClaimStepAsync(
            string executionId,
            string stepName,
            string workerId,
            CancellationToken cancellationToken)
        {
            return await _services.ObservabilityService.Tracer.TraceStorageAsync(
                new AiStorageTraceContext
                {
                    ExecutionId = executionId,
                    Backend = "Redis",
                    Operation = "TryClaimStep"
                },
                async trace =>
                {
                    var result = await _services.DagStore!.TryClaimStepAsync(
                        executionId,
                        stepName,
                        workerId,
                        cancellationToken).ConfigureAwait(false);

                    trace.SetTag("claimAcquired", result is not null);
                    trace.SetTag("workerId", workerId);
                    trace.SetTag("stepId", stepName);

                    if (result is not null)
                    {
                        trace.SetTag("claimToken", result.ClaimToken);
                    }

                    return result;
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Logs a throttled ready step candidate.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable pipeline key.
        /// </param>
        /// <param name="stepName">
        /// The throttled step name.
        /// </param>
        /// <param name="workerId">
        /// The worker identifier.
        /// </param>
        /// <param name="decision">
        /// The denied concurrency decision.
        /// </param>
        private void LogThrottledStep(
            string executionId,
            string pipelineKey,
            string stepName,
            string workerId,
            AiConcurrencyDecision decision)
        {
            _services.Logger.Engine.LogInformation(
                $"[AI DAG] Step throttled. ExecutionId='{executionId}', PipelineKey='{pipelineKey}', StepName='{stepName}', Worker='{workerId}', Reason='{decision.Reason ?? "Concurrency limit reached."}'.");
        }

        /// <summary>
        /// Recovers timed-out running DAG steps through the distributed store.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="workerId">
        /// The worker identifier.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The number of recovered steps.
        /// </returns>
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