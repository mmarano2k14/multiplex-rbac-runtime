using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Control;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;

namespace Multiplexed.AI.Runtime.Execution.Engine.Steps
{
    /// <summary>
    /// Coordinates distributed DAG step recovery and concurrency-aware claim acquisition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service is responsible for selecting ready DAG step candidates, evaluating
    /// policy-aware concurrency admission, acquiring distributed concurrency capacity,
    /// and then atomically claiming admitted steps through the distributed DAG store.
    /// </para>
    ///
    /// <para>
    /// Concurrency admission is resolved from both pipeline-level and step-level
    /// configuration. This allows pipeline-level throttle policies to apply to all
    /// matching steps without copying pipeline configuration into execution state.
    /// </para>
    ///
    /// <para>
    /// The admission preparation is centralized through
    /// <see cref="AiDagExecutionHelpers.CreateConcurrencyAdmission"/> so single-step and batch
    /// claiming use the same concurrency context and effective concurrency definition.
    /// </para>
    ///
    /// <para>
    /// The admission flow is intentionally ordered:
    /// </para>
    ///
    /// <list type="number">
    /// <item><description>Resolve pipeline-level and step-level concurrency configuration.</description></item>
    /// <item><description>Create the distributed concurrency context.</description></item>
    /// <item><description>Apply matching generic throttle rules using the runtime context.</description></item>
    /// <item><description>Evaluate configured concurrency policies when present.</description></item>
    /// <item><description>Acquire distributed Redis concurrency capacity.</description></item>
    /// <item><description>Claim DAG step ownership.</description></item>
    /// </list>
    ///
    /// <para>
    /// If policy admission is denied, Redis capacity is not acquired and the step is not claimed.
    /// </para>
    ///
    /// <para>
    /// If Redis capacity is acquired but the DAG claim fails because another worker won the claim race,
    /// the concurrency lease is released immediately using the same effective definition that was used
    /// for acquisition.
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
        /// <param name="pipeline">
        /// The resolved pipeline definition used to apply pipeline-level concurrency configuration.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable logical pipeline key used for distributed pipeline-level throttling.
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
        public async Task<AiClaimedStep?> ClaimNextAsync(
            string executionId,
            ResolvedAiPipeline pipeline,
            string pipelineKey,
            string workerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

            if (_services.DagStore is null)
            {
                throw new InvalidOperationException("Distributed DAG store is not configured.");
            }

            if (!await CanAdvanceExecutionAsync(executionId, workerId, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var recoveredCount = await RecoverTimedOutStepsAsync(
                    executionId,
                    workerId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (recoveredCount > 0)
            {
                _services.Logger.Engine.StepsRecovered(
                    executionId,
                    recoveredCount);

                _services.Logger.Engine.LogInformation(
                    $"[AI DAG] Timed-out steps recovered. ExecutionId='{executionId}', RecoveredCount='{recoveredCount}'.");
            }

            var state = await _services.DagStore.GetStateAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (state is null || state.Steps.Count == 0)
            {
                return null;
            }

            var readySteps = await _services.DagStore.GetReadyStepsAsync(
                    executionId,
                    maxSteps: 16,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var readyStep in readySteps)
            {
                if (!state.Steps.TryGetValue(readyStep.StepName, out var stepState))
                {
                    continue;
                }

                var stepDefinition = FindPipelineStep(
                    pipeline,
                    readyStep.StepName);

                var concurrencyAdmission = AiDagExecutionHelpers.CreateConcurrencyAdmission(
                    executionId,
                    pipelineKey,
                    readyStep.StepName,
                    workerId,
                    stepState,
                    pipeline.Config,
                    stepDefinition,
                    ConcurrencyDefinitionResolver);

                var concurrencyContext = concurrencyAdmission.Context;
                var concurrencyDefinition = concurrencyAdmission.Definition;

                var gateDecision = await TryAcquireConcurrencyLeaseAsync(
                        concurrencyContext,
                        concurrencyDefinition,
                        state,
                        stepState,
                        stepDefinition,
                        readyStep.StepName,
                        cancellationToken)
                    .ConfigureAwait(false);

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
                        cancellationToken)
                    .ConfigureAwait(false);

                if (claimed is null)
                {
                    await _services.ConcurrencyGate.ReleaseAsync(
                            concurrencyContext,
                            concurrencyDefinition,
                            cancellationToken)
                        .ConfigureAwait(false);

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
        /// <param name="pipeline">
        /// The resolved pipeline definition used to apply pipeline-level concurrency configuration.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable logical pipeline key used for distributed pipeline-level throttling.
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
        public async Task<IReadOnlyList<AiClaimedStep>> ClaimBatchAsync(
            string executionId,
            ResolvedAiPipeline pipeline,
            string pipelineKey,
            string workerId,
            int maxSteps,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(pipeline);
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
                    cancellationToken)
                .ConfigureAwait(false);

            var state = await _services.DagStore.GetStateAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (state is null || state.Steps.Count == 0)
            {
                return Array.Empty<AiClaimedStep>();
            }

            var readySteps = await _services.DagStore.GetReadyStepsAsync(
                    executionId,
                    maxSteps,
                    cancellationToken)
                .ConfigureAwait(false);

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

                var stepDefinition = FindPipelineStep(
                    pipeline,
                    readyStep.StepName);

                var concurrencyAdmission = AiDagExecutionHelpers.CreateConcurrencyAdmission(
                    executionId,
                    pipelineKey,
                    readyStep.StepName,
                    workerId,
                    stepState,
                    pipeline.Config,
                    stepDefinition,
                    ConcurrencyDefinitionResolver);

                var concurrencyContext = concurrencyAdmission.Context;
                var concurrencyDefinition = concurrencyAdmission.Definition;

                var gateDecision = await TryAcquireConcurrencyLeaseAsync(
                        concurrencyContext,
                        concurrencyDefinition,
                        state,
                        stepState,
                        stepDefinition,
                        readyStep.StepName,
                        cancellationToken)
                    .ConfigureAwait(false);

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
                        cancellationToken)
                    .ConfigureAwait(false);

                if (claimed is null)
                {
                    await _services.ConcurrencyGate.ReleaseAsync(
                            concurrencyContext,
                            concurrencyDefinition,
                            cancellationToken)
                        .ConfigureAwait(false);

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
        /// The effective concurrency definition after applying matching throttle rules.
        /// </param>
        /// <param name="state">
        /// The current execution state.
        /// </param>
        /// <param name="stepState">
        /// The current step state.
        /// </param>
        /// <param name="stepDefinition">
        /// The resolved pipeline step definition.
        /// </param>
        /// <param name="stepName">
        /// The ready step name.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The final concurrency decision.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Configured concurrency policies are evaluated first through the policy engine factory.
        /// If policy admission is denied, the Redis concurrency gate is not called.
        /// </para>
        ///
        /// <para>
        /// If no concurrency policies are configured, policy-engine construction is skipped and
        /// the method proceeds directly to Redis distributed admission. This preserves the existing
        /// fast path and avoids requiring a step policy context when policies are not used.
        /// </para>
        /// </remarks>
        private async Task<AiConcurrencyDecision> TryAcquireConcurrencyLeaseAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            AiExecutionState state,
            AiStepState stepState,
            AiPipelineStepDefinition stepDefinition,
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
                        trace.SetTag("concurrency.pipelineKey", context.PipelineKey);
                        trace.SetTag("concurrency.stepKey", context.StepKey);
                        trace.SetTag("concurrency.leaseId", context.LeaseId);
                        trace.SetTag("concurrency.provider", context.Provider ?? string.Empty);
                        trace.SetTag("concurrency.model", context.Model ?? string.Empty);
                        trace.SetTag("concurrency.operation", context.Operation ?? string.Empty);
                        trace.SetTag("workerId", context.RuntimeInstanceId);

                        var policyDecision = await EvaluateConfiguredConcurrencyPoliciesAsync(
                                context,
                                definition,
                                state,
                                stepState,
                                stepDefinition,
                                stepName,
                                cancellationToken)
                            .ConfigureAwait(false);

                        trace.SetTag("concurrency.policy.allowed", policyDecision.Allowed);

                        if (!policyDecision.Allowed)
                        {
                            trace.SetTag("concurrency.allowed", false);
                            trace.SetTag("concurrency.denied", true);
                            trace.SetTag("concurrency.policy.denied", true);
                            trace.SetTag("concurrency.reason", policyDecision.Reason ?? "Concurrency policy denied execution.");

                            return policyDecision;
                        }

                        var gateDecision = await _services.ConcurrencyGate.TryAcquireAsync(
                                context,
                                definition,
                                cancellationToken)
                            .ConfigureAwait(false);

                        trace.SetTag("concurrency.allowed", gateDecision.Allowed);
                        trace.SetTag("concurrency.gate.allowed", gateDecision.Allowed);

                        if (!gateDecision.Allowed)
                        {
                            trace.SetTag("concurrency.denied", true);
                            trace.SetTag("concurrency.reason", gateDecision.Reason ?? "Concurrency limit reached.");
                        }

                        return gateDecision;
                    })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates configured concurrency policies for the ready step candidate.
        /// </summary>
        /// <param name="context">
        /// The concurrency context.
        /// </param>
        /// <param name="definition">
        /// The effective concurrency definition.
        /// </param>
        /// <param name="state">
        /// The execution state.
        /// </param>
        /// <param name="stepState">
        /// The step state.
        /// </param>
        /// <param name="stepDefinition">
        /// The resolved pipeline step definition.
        /// </param>
        /// <param name="stepName">
        /// The step name.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The policy-level concurrency decision.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The policy engine is only created when at least one concurrency policy is configured.
        /// This avoids adding policy-engine dependencies to the fast path where throttling is purely
        /// config-driven or Redis-gate-driven.
        /// </para>
        /// </remarks>
        private async Task<AiConcurrencyDecision> EvaluateConfiguredConcurrencyPoliciesAsync(
            AiConcurrencyContext context,
            AiConcurrencyDefinition definition,
            AiExecutionState state,
            AiStepState stepState,
            AiPipelineStepDefinition stepDefinition,
            string stepName,
            CancellationToken cancellationToken)
        {
            if (definition.Policies.Count == 0)
            {
                return AiConcurrencyDecision.Allow();
            }

            var stepContext = CreateStepExecutionContext(
                context.ExecutionId,
                state,
                stepState,
                stepDefinition,
                stepName,
                cancellationToken);

            var policyEngine = _services.PolicyEngineFactory.Create(
                AiPolicyKind.Concurrency,
                stepContext);

            if (policyEngine is not IAiConcurrencyEngine concurrencyEngine)
            {
                throw new InvalidOperationException(
                    $"Policy engine for kind '{AiPolicyKind.Concurrency}' must implement {nameof(IAiConcurrencyEngine)}.");
            }

            return await concurrencyEngine.DecideAsync(
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a minimal step execution context for policy-engine evaluation during admission.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="state">
        /// The execution state.
        /// </param>
        /// <param name="stepState">
        /// The step state.
        /// </param>
        /// <param name="stepDefinition">
        /// The resolved pipeline step definition.
        /// </param>
        /// <param name="stepName">
        /// The step name.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A step-scoped execution context.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This context is used only for policy-engine evaluation before Redis admission.
        /// It does not execute the step.
        /// </para>
        ///
        /// <para>
        /// The resolved pipeline step carries the step identity and configuration required by
        /// policy engines that read from <see cref="AiStepExecutionContext.StepState"/>.
        /// </para>
        /// </remarks>
        private AiStepExecutionContext CreateStepExecutionContext(
            string executionId,
            AiExecutionState state,
            AiStepState stepState,
            AiPipelineStepDefinition stepDefinition,
            string stepName,
            CancellationToken cancellationToken)
        {
            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = state.PipelineName,
                ExecutionMode = AiExecutionMode.Dag
            };

            var executionContext = new AiExecutionContext(
                record,
                state,
                _services.Services,
                _services.StateReader,
                _services.StateWriter,
                cancellationToken);

            var resolvedStep = new ResolvedAiPipelineStep
            {
                Name = stepName,
                StepKey = string.IsNullOrWhiteSpace(stepDefinition.StepKey)
                    ? stepName
                    : stepDefinition.StepKey,
                Config = stepDefinition.Config ?? stepState.Config ?? new Dictionary<string, object?>()
            };

            return new AiStepExecutionContext(
                executionContext,
                resolvedStep);
        }

        /// <summary>
        /// Finds the pipeline step definition for a ready step.
        /// </summary>
        /// <param name="pipeline">
        /// The resolved pipeline definition.
        /// </param>
        /// <param name="stepName">
        /// The ready step name.
        /// </param>
        /// <returns>
        /// The matching pipeline step definition, or a minimal fallback definition when the step
        /// cannot be found.
        /// </returns>
        private static AiPipelineStepDefinition FindPipelineStep(
            ResolvedAiPipeline pipeline,
            string stepName)
        {
            var step = pipeline.Steps.FirstOrDefault(x =>
                string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase));

            if (step is not null)
            {
                return new AiPipelineStepDefinition
                {
                    Name = step.Name,
                    StepKey = step.StepKey,
                    Config = step.Config ?? new Dictionary<string, object?>(),
                    DependsOn = step.DependsOn ?? Array.Empty<string>()
                };
            }

            return new AiPipelineStepDefinition
            {
                Name = stepName,
                StepKey = stepName,
                Config = new Dictionary<string, object?>(),
                DependsOn = Array.Empty<string>()
            };
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
                                cancellationToken)
                            .ConfigureAwait(false);

                        trace.SetTag("claimAcquired", result is not null);
                        trace.SetTag("workerId", workerId);
                        trace.SetTag("stepId", stepName);

                        if (result is not null)
                        {
                            trace.SetTag("claimToken", result.ClaimToken);
                        }

                        return result;
                    })
                .ConfigureAwait(false);
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
                                cancellationToken)
                            .ConfigureAwait(false);

                        trace.SetTag("recoveredCount", result);
                        trace.SetTag("workerId", workerId);
                        trace.SetTag("recovered", result > 0);

                        return result;
                    })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Determines whether the execution is currently allowed to claim or advance work.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="workerId">The runtime worker identifier.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns><c>true</c> when the execution may advance; otherwise, <c>false</c>.</returns>
        private async Task<bool> CanAdvanceExecutionAsync(
            string executionId,
            string workerId,
            CancellationToken cancellationToken)
        {
            var decision = await _services.ExecutionControlGate
                .CheckBeforeAdvanceAsync(executionId, cancellationToken)
                .ConfigureAwait(false);

            if (decision.CanContinue)
            {
                return true;
            }

            if (decision.Status == AiExecutionControlStatus.Pausing)
            {
                await MarkPausedIfExecutionHasDrainedAsync(
                        executionId,
                        workerId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            _services.Logger.Engine.LogInformation(
                $"[AI DAG] Execution advancement blocked by control state. ExecutionId='{executionId}', WorkerId='{workerId}', Status='{decision.Status}', StopClaiming='{decision.ShouldStopClaiming}', ShouldCancel='{decision.ShouldCancel}', WaitingForInput='{decision.IsWaitingForInput}', Reason='{decision.Reason}'.");

            return false;
        }

        /// <summary>
        /// Marks an execution as effectively paused when no active claimed or running work remains.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="workerId">The runtime worker identifier observing the drained state.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        private async Task MarkPausedIfExecutionHasDrainedAsync(
            string executionId,
            string workerId,
            CancellationToken cancellationToken)
        {
            if (_services.DagStore is null)
            {
                return;
            }

            var state = await _services.DagStore.GetStateAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (state is null)
            {
                return;
            }

            var hasActiveWork = state.Steps.Values.Any(step =>
                step.Status == AiStepExecutionStatus.Running ||
                !string.IsNullOrWhiteSpace(step.ClaimedBy) ||
                !string.IsNullOrWhiteSpace(step.ClaimToken) ||
                step.ClaimedAtUtc.HasValue ||
                step.LeaseExpiresAtUtc.HasValue);

            if (hasActiveWork)
            {
                return;
            }

            var paused = await _services.ExecutionControlService
                .MarkPausedAsync(
                    executionId,
                    requestedBy: workerId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (paused.Status == AiExecutionControlStatus.Paused)
            {
                _services.Logger.Engine.LogInformation(
                    $"[AI DAG] Execution pause completed after active work drained. ExecutionId='{executionId}', WorkerId='{workerId}', ControlStatus='{paused.Status}'.");
            }
        }
    }
}
