using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Engine.Retention
{
    /// <summary>
    /// Coordinates policy-driven retention for DAG execution state.
    ///
    /// PURPOSE:
    /// - Apply retention policies after step lifecycle mutations.
    /// - Persist the updated distributed execution state.
    /// - Warm the archive-aware resolver for evicted steps.
    ///
    /// IMPORTANT:
    /// - This coordinator does not execute steps.
    /// - This coordinator does not evaluate convergence.
    /// - This coordinator does not finalize executions.
    /// </summary>
    public sealed class AiDagRetentionCoordinator
    {
        private readonly IAiDagExecutionEngineServices _engineServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagRetentionCoordinator"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services.
        /// </param>
        public AiDagRetentionCoordinator(
            IAiDagExecutionEngineServices engineServices)
        {
            _engineServices = engineServices;
        }

        /// <summary>
        /// Applies retention, persists state, and warms the resolver cache.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="state">
        /// The current execution state.
        /// </param>
        /// <param name="stepContext">
        /// The current step context used to resolve retention policies.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task ApplyRetentionPersistAndWarmAsync(
            string executionId,
            AiExecutionState state,
            AiStepExecutionContext stepContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepContext);

            await _engineServices.ObservabilityService.Tracer.TraceRetentionAsync(
                new AiRetentionTraceContext
                {
                    ExecutionId = executionId,
                    PolicyName = "policy-driven-retention",
                    InspectedSteps = state.Steps.Count
                },
                async trace =>
                {
                    var stepsBefore = state.Steps.Count;

                    _engineServices.ObservabilityService.Metrics.Retention.Trigger.RecordTriggered(
                        executionId,
                        "retention-invoked");

                    var result = await _engineServices.PolicyEngineFactory
                        .Create<IAiRetentionEngine>(AiPolicyKind.Retention, stepContext)
                        .ApplyAsync(
                            new AiRetentionContext
                            {
                                ExecutionId = executionId,
                                ExecutionState = state,
                                UtcNow = DateTime.UtcNow
                            },
                            cancellationToken)
                        .ConfigureAwait(false);

                    var evictedSteps = result.EvictedSteps ?? Array.Empty<string>();

                    if (result.IsEmpty)
                    {
                        _engineServices.ObservabilityService.Metrics.Retention.Trigger.RecordSkipped(
                            executionId,
                            result.Decision.Reason ?? "no-policy-or-no-op");

                        trace.SetTag("skipped", true);
                        trace.SetTag("reason", result.Decision.Reason ?? "no-policy-or-no-op");
                    }
                    else
                    {
                        var compactedCount = result.CompactedSteps?.Count ?? 0;
                        var evictedCount = evictedSteps.Count;

                        trace.SetTag("skipped", false);
                        trace.SetTag("compactedCount", compactedCount);
                        trace.SetTag("evictedCount", evictedCount);
                        trace.SetTag("totalSteps", state.Steps.Count);

                        if (compactedCount > 0)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Decision.RecordCompactionRequired(
                                executionId,
                                state.Steps.Count,
                                compactedCount);
                        }

                        if (evictedCount > 0)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Decision.RecordEvictionRequired(
                                executionId,
                                state.Steps.Count,
                                evictedCount);
                        }

                        if (compactedCount == 0 && evictedCount == 0)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Decision.RecordNoActionRequired(
                                executionId,
                                state.Steps.Count);
                        }

                        _engineServices.ObservabilityService.Metrics.Retention.Plan.RecordPlanCreated(
                            executionId,
                            compactedCount,
                            evictedCount,
                            state.Steps.Count);

                        foreach (var stepId in evictedSteps)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordStepEvicted(
                                executionId,
                                stepId);

                            _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordStepMarkedArchived(
                                executionId,
                                stepId);
                        }

                        if (result.CompactedSteps is not null)
                        {
                            foreach (var stepId in result.CompactedSteps)
                            {
                                _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordPayloadCompacted(
                                    executionId,
                                    stepId,
                                    beforeBytes: 0,
                                    afterBytes: 0);
                            }
                        }

                        _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordRetentionCompleted(
                            executionId);
                    }

                    if (_engineServices.DagStore is not null)
                    {
                        await _engineServices.DagStore.SaveStateAsync(
                            executionId,
                            state,
                            cancellationToken).ConfigureAwait(false);

                        trace.SetTag("statePersisted", true);
                    }
                    else
                    {
                        trace.SetTag("statePersisted", false);
                    }

                    if (evictedSteps.Count > 0)
                    {
                        await _engineServices.StepResolver.WarmStepsAsync(
                            executionId,
                            state,
                            evictedSteps,
                            cancellationToken).ConfigureAwait(false);

                        trace.SetTag("resolverWarmed", true);
                        trace.SetTag("resolverWarmStepCount", evictedSteps.Count);
                    }
                    else
                    {
                        trace.SetTag("resolverWarmed", false);
                        trace.SetTag("resolverWarmStepCount", 0);
                    }

                    var stepsAfter = state.Steps.Count;

                    trace.SetTag("stepsBefore", stepsBefore);
                    trace.SetTag("stepsAfter", stepsAfter);
                    trace.SetTag("removedSteps", stepsBefore - stepsAfter);
                    trace.SetTag("workerId", _engineServices.RuntimeInstanceIdentity.RuntimeInstanceId);

                    return true;
                });
        }


        /// <summary>
        /// Applies retention once for a completed batch of DAG step transitions, persists state,
        /// and warms the resolver for all steps that may be needed after batch retention.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="state">The current execution state after all batch step transitions have been persisted.</param>
        /// <param name="stepContext">A representative step context used to resolve retention policies.</param>
        /// <param name="candidateStepIds">
        /// Step identifiers that should remain resolvable after retention. This should include
        /// transitioned batch steps and any required completed steps captured before retention.
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ApplyBatchRetentionPersistAndWarmAsync(
            string executionId,
            AiExecutionState state,
            AiStepExecutionContext stepContext,
            IReadOnlyCollection<string> candidateStepIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepContext);
            ArgumentNullException.ThrowIfNull(candidateStepIds);

            await _engineServices.ObservabilityService.Tracer.TraceRetentionAsync(
                new AiRetentionTraceContext
                {
                    ExecutionId = executionId,
                    PolicyName = "policy-driven-batch-retention",
                    InspectedSteps = state.Steps.Count
                },
                async trace =>
                {
                    var stepsBefore = state.Steps.Count;

                    _engineServices.ObservabilityService.Metrics.Retention.Trigger.RecordTriggered(
                        executionId,
                        "batch-retention-invoked");

                    var result = await _engineServices.PolicyEngineFactory
                        .Create<IAiRetentionEngine>(AiPolicyKind.Retention, stepContext)
                        .ApplyAsync(
                            new AiRetentionContext
                            {
                                ExecutionId = executionId,
                                ExecutionState = state,
                                UtcNow = DateTime.UtcNow
                            },
                            cancellationToken)
                        .ConfigureAwait(false);

                    var evictedSteps = result.EvictedSteps ?? Array.Empty<string>();
                    var compactedSteps = result.CompactedSteps ?? Array.Empty<string>();

                    if (result.IsEmpty)
                    {
                        _engineServices.ObservabilityService.Metrics.Retention.Trigger.RecordSkipped(
                            executionId,
                            result.Decision.Reason ?? "no-policy-or-no-op");

                        trace.SetTag("skipped", true);
                        trace.SetTag("reason", result.Decision.Reason ?? "no-policy-or-no-op");
                    }
                    else
                    {
                        var compactedCount = compactedSteps.Count;
                        var evictedCount = evictedSteps.Count;

                        trace.SetTag("skipped", false);
                        trace.SetTag("compactedCount", compactedCount);
                        trace.SetTag("evictedCount", evictedCount);
                        trace.SetTag("totalSteps", state.Steps.Count);

                        if (compactedCount > 0)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Decision.RecordCompactionRequired(
                                executionId,
                                state.Steps.Count,
                                compactedCount);
                        }

                        if (evictedCount > 0)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Decision.RecordEvictionRequired(
                                executionId,
                                state.Steps.Count,
                                evictedCount);
                        }

                        if (compactedCount == 0 && evictedCount == 0)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Decision.RecordNoActionRequired(
                                executionId,
                                state.Steps.Count);
                        }

                        _engineServices.ObservabilityService.Metrics.Retention.Plan.RecordPlanCreated(
                            executionId,
                            compactedCount,
                            evictedCount,
                            state.Steps.Count);

                        foreach (var stepId in evictedSteps)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordStepEvicted(
                                executionId,
                                stepId);

                            _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordStepMarkedArchived(
                                executionId,
                                stepId);
                        }

                        foreach (var stepId in compactedSteps)
                        {
                            _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordPayloadCompacted(
                                executionId,
                                stepId,
                                beforeBytes: 0,
                                afterBytes: 0);
                        }

                        _engineServices.ObservabilityService.Metrics.Retention.Execution.RecordRetentionCompleted(
                            executionId);
                    }

                    if (_engineServices.DagStore is not null)
                    {
                        await _engineServices.DagStore.SaveStateAsync(
                            executionId,
                            state,
                            cancellationToken).ConfigureAwait(false);

                        trace.SetTag("statePersisted", true);
                    }
                    else
                    {
                        trace.SetTag("statePersisted", false);
                    }

                    var warmStepIds = new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var stepId in candidateStepIds)
                    {
                        if (!string.IsNullOrWhiteSpace(stepId))
                        {
                            warmStepIds.Add(stepId);
                        }
                    }

                    foreach (var stepId in evictedSteps)
                    {
                        if (!string.IsNullOrWhiteSpace(stepId))
                        {
                            warmStepIds.Add(stepId);
                        }
                    }

                    foreach (var stepId in compactedSteps)
                    {
                        if (!string.IsNullOrWhiteSpace(stepId))
                        {
                            warmStepIds.Add(stepId);
                        }
                    }

                    if (warmStepIds.Count > 0)
                    {
                        await _engineServices.StepResolver.WarmStepsAsync(
                            executionId,
                            state,
                            warmStepIds.ToArray(),
                            cancellationToken).ConfigureAwait(false);

                        trace.SetTag("resolverWarmed", true);
                        trace.SetTag("resolverWarmStepCount", warmStepIds.Count);
                    }
                    else
                    {
                        trace.SetTag("resolverWarmed", false);
                        trace.SetTag("resolverWarmStepCount", 0);
                    }

                    var stepsAfter = state.Steps.Count;

                    trace.SetTag("stepsBefore", stepsBefore);
                    trace.SetTag("stepsAfter", stepsAfter);
                    trace.SetTag("removedSteps", stepsBefore - stepsAfter);
                    trace.SetTag("workerId", _engineServices.RuntimeInstanceIdentity.RuntimeInstanceId);

                    return true;
                }).ConfigureAwait(false);
        }


    }
}