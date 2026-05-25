using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Execution.Engine.Models;
using Multiplexed.AI.Runtime.Execution.Engine.Retention;

namespace Multiplexed.AI.Runtime.Execution.Engine.Finalization
{
    /// <summary>
    /// Coordinates distributed execution convergence persistence and terminal finalization.
    /// </summary>
    /// <remarks>
    /// This service is responsible for persisting converged execution records in a
    /// distributed-safe manner. It also applies durable execution control state before
    /// terminal finalization so that cancellation requests take precedence over normal
    /// DAG completion.
    /// </remarks>
    public sealed class AiDagExecutionFinalizationService
    {
        private readonly IAiDagExecutionEngineServices _engineServices;
        private readonly AiDagRetentionCoordinator _retentionCoordinator;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionFinalizationService"/> class.
        /// </summary>
        /// <param name="engineServices">The DAG execution engine services.</param>
        /// <param name="retentionCoordinator">The retention coordinator.</param>
        public AiDagExecutionFinalizationService(
            IAiDagExecutionEngineServices engineServices,
            AiDagRetentionCoordinator retentionCoordinator)
        {
            _engineServices = engineServices ?? throw new ArgumentNullException(nameof(engineServices));
            _retentionCoordinator = retentionCoordinator ?? throw new ArgumentNullException(nameof(retentionCoordinator));
        }

        /// <summary>
        /// Persists a converged execution record in a distributed-safe manner.
        /// </summary>
        /// <param name="record">The execution record projection to persist.</param>
        /// <param name="convergence">The evaluated convergence result.</param>
        /// <param name="expectedStepKey">The optimistic execution step key.</param>
        /// <param name="state">The authoritative execution state.</param>
        /// <param name="resolvedPipeline">The resolved pipeline.</param>
        /// <param name="buildExecutionContext">Factory used to build execution contexts.</param>
        /// <param name="persistAsync">Fallback persistence delegate for non-distributed execution.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous persistence operation.</returns>
        public async Task PersistDistributedConvergedRecordAsync(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            string expectedStepKey,
            AiExecutionState state,
            ResolvedAiPipeline resolvedPipeline,
            Func<AiExecutionRecord, AiExecutionState, CancellationToken, AiExecutionContext> buildExecutionContext,
            Func<AiExecutionRecord, string, AiExecutionState, CancellationToken, Task> persistAsync,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(convergence);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedStepKey);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(resolvedPipeline);
            ArgumentNullException.ThrowIfNull(buildExecutionContext);
            ArgumentNullException.ThrowIfNull(persistAsync);

            var utcNow = DateTime.UtcNow;
            var pipelineKey = $"{resolvedPipeline.Name}:{resolvedPipeline.Version}";
            var runtimeInstanceId = _engineServices.RuntimeInstanceIdentity.RuntimeInstanceId;

            if (_engineServices.DagStore is null)
            {
                record.TouchVersion();
                record.RenewExecutionStepKey();

                await persistAsync(
                        record,
                        expectedStepKey,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                return;
            }

            var completedSteps = record.CompletedSteps
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (convergence.IsTerminal)
            {
                var finalStatus = await ResolveFinalStatusAsync(
                        record.ExecutionId,
                        convergence.Status,
                        cancellationToken)
                    .ConfigureAwait(false);

                await RecordFinalizationStartedAsync(
                        record,
                        convergence,
                        finalStatus,
                        expectedStepKey,
                        resolvedPipeline,
                        completedSteps.Count,
                        pipelineKey,
                        runtimeInstanceId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (finalStatus == AiExecutionStatus.Cancelled &&
                    convergence.Status != AiExecutionStatus.Cancelled)
                {
                    await RecordCancellationOverrideAppliedAsync(
                            record,
                            convergence,
                            finalStatus,
                            expectedStepKey,
                            resolvedPipeline,
                            pipelineKey,
                            runtimeInstanceId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI DAG] Finalization attempt. ExecutionId='{record.ExecutionId}', Status='{finalStatus}', ExpectedStepKey='{expectedStepKey}'.");

                try
                {
                    var retentionStep = resolvedPipeline.Steps.FirstOrDefault()
                        ?? throw new InvalidOperationException(
                            $"Pipeline '{resolvedPipeline.Name}' does not contain any resolved step for retention context creation.");

                    var executionContext = buildExecutionContext(
                        record,
                        state,
                        cancellationToken);

                    var retentionStepContext = new AiStepExecutionContext(
                        executionContext,
                        retentionStep);

                    await _retentionCoordinator.ApplyRetentionPersistAndWarmAsync(
                            record.ExecutionId,
                            state,
                            retentionStepContext,
                            cancellationToken)
                        .ConfigureAwait(false);

                    var request = new AiDagExecutionFinalizationRequest
                    {
                        ExecutionId = record.ExecutionId,
                        ExpectedExecutionStepKey = expectedStepKey,
                        Status = finalStatus,
                        CompletedAtUtc = utcNow,
                        CompletedSteps = completedSteps,
                        CurrentStep = string.Empty,
                        WorkerId = runtimeInstanceId
                    };

                    var success = await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
                        new AiStorageTraceContext
                        {
                            ExecutionId = record.ExecutionId,
                            Backend = "Redis",
                            Operation = "TryFinalizeExecution"
                        },
                        async trace =>
                        {
                            var result = await _engineServices.DagStore.TryFinalizeExecutionAsync(
                                    request,
                                    cancellationToken)
                                .ConfigureAwait(false);

                            trace.SetTag("finalized", result);
                            trace.SetTag("status", finalStatus.ToString());
                            trace.SetTag("workerId", runtimeInstanceId);
                            trace.SetTag("expectedStepKey", expectedStepKey);
                            trace.SetTag("completedSteps", completedSteps.Count);

                            return result;
                        }).ConfigureAwait(false);

                    if (!success)
                    {
                        await RecordFinalizationRaceLostAsync(
                                record,
                                finalStatus,
                                expectedStepKey,
                                resolvedPipeline,
                                completedSteps.Count,
                                pipelineKey,
                                runtimeInstanceId,
                                "Finalization race lost. Another worker finalized or updated the execution first.",
                                cancellationToken)
                            .ConfigureAwait(false);

                        _engineServices.Logger.Engine.FinalizationRaceLost(
                            record.ExecutionId,
                            finalStatus);

                        var refreshed = await _engineServices.DagStore.GetRecordAsync(
                                record.ExecutionId,
                                cancellationToken)
                            .ConfigureAwait(false);

                        if (refreshed is not null)
                        {
                            AiDagExecutionRecordFinalizer.ApplyAuthoritativeRecord(
                                record,
                                refreshed);
                        }

                        return;
                    }

                    await RecordFinalizationCompletedAsync(
                            record,
                            finalStatus,
                            expectedStepKey,
                            resolvedPipeline,
                            completedSteps.Count,
                            pipelineKey,
                            runtimeInstanceId,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await RecordExecutionTerminalEventsAsync(
                            record,
                            finalStatus,
                            resolvedPipeline,
                            completedSteps.Count,
                            pipelineKey,
                            runtimeInstanceId,
                            cancellationToken)
                        .ConfigureAwait(false);

                    _engineServices.Logger.Engine.FinalizationSucceeded(
                        record.ExecutionId,
                        finalStatus);

                    var updated = await _engineServices.DagStore.GetRecordAsync(
                            record.ExecutionId,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (updated is not null)
                    {
                        AiDagExecutionRecordFinalizer.ApplyAuthoritativeRecord(
                            record,
                            updated);
                    }

                    return;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await RecordFinalizationFailedAsync(
                            record,
                            finalStatus,
                            expectedStepKey,
                            resolvedPipeline,
                            completedSteps.Count,
                            pipelineKey,
                            runtimeInstanceId,
                            exception.Message,
                            cancellationToken)
                        .ConfigureAwait(false);

                    throw;
                }
            }

            record.TouchVersion();
            record.RenewExecutionStepKey();

            await _engineServices.DagStore.SaveRecordAsync(
                    record,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the terminal execution status by applying durable execution control state.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="convergedStatus">The status produced by DAG convergence.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The final terminal status that should be persisted.</returns>
        /// <remarks>
        /// Cancellation control has precedence over normal completion. This prevents an
        /// execution that has received a distributed cancellation request from converging
        /// as completed simply because already-claimed work finished safely.
        /// </remarks>
        private async Task<AiExecutionStatus> ResolveFinalStatusAsync(
            string executionId,
            AiExecutionStatus convergedStatus,
            CancellationToken cancellationToken)
        {
            if (convergedStatus == AiExecutionStatus.Cancelled)
            {
                return AiExecutionStatus.Cancelled;
            }

            if (_engineServices.ExecutionControlGate is null)
            {
                return convergedStatus;
            }

            var decision = await _engineServices.ExecutionControlGate
                .CheckBeforeAdvanceAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (decision is null || !decision.ShouldCancel)
            {
                return convergedStatus;
            }

            _engineServices.Logger.Engine.LogInformation(
                $"[AI DAG] Final status overridden by execution control cancellation. ExecutionId='{executionId}', ConvergedStatus='{convergedStatus}', ControlStatus='{decision.Status}', Reason='{decision.Reason}'.");

            return AiExecutionStatus.Cancelled;
        }

        /// <summary>
        /// Records a finalization started ledger event.
        /// </summary>
        private async Task RecordFinalizationStartedAsync(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            AiExecutionStatus finalStatus,
            string expectedStepKey,
            ResolvedAiPipeline resolvedPipeline,
            int completedStepsCount,
            string pipelineKey,
            string runtimeInstanceId,
            CancellationToken cancellationToken)
        {
            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _engineServices,
                    record.ExecutionId,
                    pipelineKey,
                    "_finalization",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Finalization,
                    AiDecisionLedgerEvents.Finalization.Started,
                    AiDecisionLedgerOutcome.Started,
                    "Execution finalization started.",
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = resolvedPipeline.Name,
                        ["pipeline.version"] = resolvedPipeline.Version,
                        ["converged.status"] = convergence.Status.ToString(),
                        ["final.status"] = finalStatus.ToString(),
                        ["expected.step.key"] = expectedStepKey,
                        ["completed.steps.count"] = completedStepsCount.ToString()
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Records a cancellation override ledger event.
        /// </summary>
        private async Task RecordCancellationOverrideAppliedAsync(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            AiExecutionStatus finalStatus,
            string expectedStepKey,
            ResolvedAiPipeline resolvedPipeline,
            string pipelineKey,
            string runtimeInstanceId,
            CancellationToken cancellationToken)
        {
            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _engineServices,
                    record.ExecutionId,
                    pipelineKey,
                    "_finalization",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Finalization,
                    AiDecisionLedgerEvents.Finalization.CancellationOverrideApplied,
                    AiDecisionLedgerOutcome.Applied,
                    "Final status overridden by execution control cancellation.",
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = resolvedPipeline.Name,
                        ["pipeline.version"] = resolvedPipeline.Version,
                        ["converged.status"] = convergence.Status.ToString(),
                        ["final.status"] = finalStatus.ToString(),
                        ["expected.step.key"] = expectedStepKey
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Records a finalization completed ledger event.
        /// </summary>
        private async Task RecordFinalizationCompletedAsync(
            AiExecutionRecord record,
            AiExecutionStatus finalStatus,
            string expectedStepKey,
            ResolvedAiPipeline resolvedPipeline,
            int completedStepsCount,
            string pipelineKey,
            string runtimeInstanceId,
            CancellationToken cancellationToken)
        {
            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _engineServices,
                    record.ExecutionId,
                    pipelineKey,
                    "_finalization",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Finalization,
                    AiDecisionLedgerEvents.Finalization.Completed,
                    AiDecisionLedgerOutcome.Completed,
                    "Execution finalization completed.",
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = resolvedPipeline.Name,
                        ["pipeline.version"] = resolvedPipeline.Version,
                        ["final.status"] = finalStatus.ToString(),
                        ["expected.step.key"] = expectedStepKey,
                        ["completed.steps.count"] = completedStepsCount.ToString()
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Records a finalization race-lost ledger event.
        /// </summary>
        private async Task RecordFinalizationRaceLostAsync(
            AiExecutionRecord record,
            AiExecutionStatus finalStatus,
            string expectedStepKey,
            ResolvedAiPipeline resolvedPipeline,
            int completedStepsCount,
            string pipelineKey,
            string runtimeInstanceId,
            string reason,
            CancellationToken cancellationToken)
        {
            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _engineServices,
                    record.ExecutionId,
                    pipelineKey,
                    "_finalization",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Finalization,
                    AiDecisionLedgerEvents.Finalization.RaceLost,
                    AiDecisionLedgerOutcome.Denied,
                    reason,
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = resolvedPipeline.Name,
                        ["pipeline.version"] = resolvedPipeline.Version,
                        ["final.status"] = finalStatus.ToString(),
                        ["expected.step.key"] = expectedStepKey,
                        ["completed.steps.count"] = completedStepsCount.ToString(),
                        ["race.type"] = "distributed-finalization"
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Records a finalization failed ledger event.
        /// </summary>
        private async Task RecordFinalizationFailedAsync(
            AiExecutionRecord record,
            AiExecutionStatus finalStatus,
            string expectedStepKey,
            ResolvedAiPipeline resolvedPipeline,
            int completedStepsCount,
            string pipelineKey,
            string runtimeInstanceId,
            string reason,
            CancellationToken cancellationToken)
        {
            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _engineServices,
                    record.ExecutionId,
                    pipelineKey,
                    "_finalization",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Finalization,
                    AiDecisionLedgerEvents.Finalization.Failed,
                    AiDecisionLedgerOutcome.Failed,
                    reason,
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = resolvedPipeline.Name,
                        ["pipeline.version"] = resolvedPipeline.Version,
                        ["final.status"] = finalStatus.ToString(),
                        ["expected.step.key"] = expectedStepKey,
                        ["completed.steps.count"] = completedStepsCount.ToString()
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Records execution terminal ledger events after successful finalization.
        /// </summary>
        private async Task RecordExecutionTerminalEventsAsync(
            AiExecutionRecord record,
            AiExecutionStatus finalStatus,
            ResolvedAiPipeline resolvedPipeline,
            int completedStepsCount,
            string pipelineKey,
            string runtimeInstanceId,
            CancellationToken cancellationToken)
        {
            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _engineServices,
                    record.ExecutionId,
                    pipelineKey,
                    "_execution",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Execution,
                    AiDecisionLedgerEvents.Execution.Finalized,
                    ToExecutionOutcome(finalStatus),
                    "Execution finalized.",
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = resolvedPipeline.Name,
                        ["pipeline.version"] = resolvedPipeline.Version,
                        ["final.status"] = finalStatus.ToString(),
                        ["completed.steps.count"] = completedStepsCount.ToString()
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _engineServices,
                    record.ExecutionId,
                    pipelineKey,
                    "_execution",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Execution,
                    ToExecutionEventType(finalStatus),
                    ToExecutionOutcome(finalStatus),
                    $"Execution {finalStatus.ToString().ToLowerInvariant()}.",
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = resolvedPipeline.Name,
                        ["pipeline.version"] = resolvedPipeline.Version,
                        ["final.status"] = finalStatus.ToString(),
                        ["completed.steps.count"] = completedStepsCount.ToString()
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Converts a terminal execution status into a decision ledger event type.
        /// </summary>
        private static string ToExecutionEventType(
            AiExecutionStatus status)
        {
            return status switch
            {
                AiExecutionStatus.Completed => AiDecisionLedgerEvents.Execution.Completed,
                AiExecutionStatus.Failed => AiDecisionLedgerEvents.Execution.Failed,
                AiExecutionStatus.Cancelled => AiDecisionLedgerEvents.Execution.Cancelled,
                _ => AiDecisionLedgerEvents.Execution.Finalized
            };
        }

        /// <summary>
        /// Converts a terminal execution status into a decision ledger outcome.
        /// </summary>
        private static AiDecisionLedgerOutcome ToExecutionOutcome(
            AiExecutionStatus status)
        {
            return status switch
            {
                AiExecutionStatus.Completed => AiDecisionLedgerOutcome.Completed,
                AiExecutionStatus.Failed => AiDecisionLedgerOutcome.Failed,
                AiExecutionStatus.Cancelled => AiDecisionLedgerOutcome.Cancelled,
                _ => AiDecisionLedgerOutcome.Completed
            };
        }
    }
}