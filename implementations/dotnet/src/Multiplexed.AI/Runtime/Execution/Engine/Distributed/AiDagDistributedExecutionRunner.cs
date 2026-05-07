using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Finalization;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Execution.Engine.Retention;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Execution.Engine.Distributed
{
    /// <summary>
    /// Executes DAG pipelines using the distributed execution path.
    /// </summary>
    public sealed class AiDagDistributedExecutionRunner
    {
        private readonly IAiDagExecutionEngineServices _engineServices;
        private readonly AiDagStepClaimService _claimService;
        private readonly AiDagClaimedStepExecutor _claimedStepExecutor;
        private readonly AiDagRetentionCoordinator _retentionCoordinator;
        private readonly AiDagExecutionFinalizationService _finalizationService;
        private readonly AiDagExecutionLifecycleHelper _lifecycleHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagDistributedExecutionRunner"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services.
        /// </param>
        /// <param name="claimService">
        /// The distributed step claim service.
        /// </param>
        /// <param name="claimedStepExecutor">
        /// The claimed step executor.
        /// </param>
        /// <param name="retentionCoordinator">
        /// The retention coordinator.
        /// </param>
        /// <param name="finalizationService">
        /// The distributed finalization service.
        /// </param>
        /// <param name="lifecycleHelper">
        /// The terminal lifecycle helper.
        /// </param>
        public AiDagDistributedExecutionRunner(
            IAiDagExecutionEngineServices engineServices,
            AiDagStepClaimService claimService,
            AiDagClaimedStepExecutor claimedStepExecutor,
            AiDagRetentionCoordinator retentionCoordinator,
            AiDagExecutionFinalizationService finalizationService,
            AiDagExecutionLifecycleHelper lifecycleHelper)
        {
            _engineServices = engineServices
                ?? throw new ArgumentNullException(nameof(engineServices));

            _claimService = claimService
                ?? throw new ArgumentNullException(nameof(claimService));

            _claimedStepExecutor = claimedStepExecutor
                ?? throw new ArgumentNullException(nameof(claimedStepExecutor));

            _retentionCoordinator = retentionCoordinator
                ?? throw new ArgumentNullException(nameof(retentionCoordinator));

            _finalizationService = finalizationService
                ?? throw new ArgumentNullException(nameof(finalizationService));

            _lifecycleHelper = lifecycleHelper
                ?? throw new ArgumentNullException(nameof(lifecycleHelper));
        }

        /// <summary>
        /// Executes the next distributed DAG step.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="loadContextAndSetAsync">
        /// Delegate used to load and set the RBAC context.
        /// </param>
        /// <param name="buildExecutionContext">
        /// Delegate used to build execution contexts.
        /// </param>
        /// <param name="persistAsync">
        /// Delegate used to persist execution state.
        /// </param>
        /// <param name="ensurePipelineName">
        /// Delegate used to validate pipeline names.
        /// </param>
        /// <param name="validateExecutionId">
        /// Delegate used to validate execution identifiers.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The updated execution record.
        /// </returns>
        public async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            Func<string, Task> loadContextAndSetAsync,
            Func<AiExecutionRecord, AiExecutionState, CancellationToken, AiExecutionContext> buildExecutionContext,
            Func<AiExecutionRecord, string, AiExecutionState, CancellationToken, Task> persistAsync,
            Action<AiExecutionRecord> ensurePipelineName,
            Action<string> validateExecutionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            validateExecutionId(executionId);

            var record = await _engineServices.DagStore!.GetRecordAsync(
                executionId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Execution '{executionId}' was not found.");

            if (record.IsTerminal)
            {
                _engineServices.Logger.Engine.ExecutionAlreadyCompleted(record);

                await _lifecycleHelper.TryCleanupIfNeededAsync(
                    record,
                    cancellationToken);

                return record;
            }

            if (record.ExecutionMode != AiExecutionMode.Dag)
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' is configured for mode '{record.ExecutionMode}' and cannot be executed by the DAG engine.");
            }

            ensurePipelineName(record);

            await loadContextAndSetAsync(record.ContextKey);

            var resolvedPipeline = await _engineServices.PipelineExecutor.PrepareAsync(
                record.PipelineName!,
                cancellationToken);

            var claimed = await _claimService.ClaimNextAsync(
                executionId,
                Environment.MachineName,
                cancellationToken);

            var state = await _engineServices.DagStore.GetStateAsync(
                executionId,
                cancellationToken)
                ?? new AiExecutionState
                {
                    ExecutionId = executionId,
                    PipelineName = record.PipelineName,
                    PipelineConfig = new Dictionary<string, object?>(
                        resolvedPipeline.Config,
                        StringComparer.Ordinal)
                };

            await _engineServices.StepResolver.WarmAsync(
                executionId,
                state,
                cancellationToken);

            try
            {
                if (claimed is null)
                {
                    record.Steps = resolvedPipeline.Steps
                        .Select(x => x.Name)
                        .ToList();

                    var convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                        resolvedPipeline,
                        state,
                        _engineServices.StateWriter,
                        _engineServices.StepResolver,
                        DateTime.UtcNow,
                        cancellationToken);

                    _engineServices.Logger.Engine.LogInformation(
                        $"[AI DAG] No distributed claim acquired. ExecutionId='{record.ExecutionId}', ConvergenceStatus='{convergence.Status}', Worker='{Environment.MachineName}'.");

                    AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                        record,
                        convergence,
                        state);

                    var expectedStepKey = AiDagExecutionHelpers.GetRequiredExecutionStepKey(record);

                    await _finalizationService.PersistDistributedConvergedRecordAsync(
                        record,
                        convergence,
                        expectedStepKey,
                        state,
                        resolvedPipeline,
                        buildExecutionContext,
                        persistAsync,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        _engineServices.Logger.Engine.ExecutionCompleted(record);

                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(
                            record.ExecutionId);

                        await _lifecycleHelper.TryPersistTerminalSnapshotAsync(
                            record,
                            state,
                            cancellationToken);

                        await _lifecycleHelper.TryCleanupIfNeededAsync(
                            record,
                            cancellationToken);
                    }

                    return record;
                }

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI DAG] Step claimed. ExecutionId='{record.ExecutionId}', StepName='{claimed.StepName}', Worker='{Environment.MachineName}', ClaimToken='{claimed.ClaimToken}'.");

                record.MarkRunning();
                record.CurrentStep = claimed.StepName;

                AiStepResult stepResult;

                try
                {
                    stepResult = await _claimedStepExecutor.ExecuteAsync(
                        record,
                        state,
                        resolvedPipeline,
                        claimed,
                        buildExecutionContext,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
                        new AiStorageTraceContext
                        {
                            ExecutionId = executionId,
                            StepId = claimed.StepName,
                            Backend = "Redis",
                            Operation = "TryFailStepException"
                        },
                        async trace =>
                        {
                            var result = await _engineServices.DagStore.TryFailStepAsync(
                                executionId,
                                claimed.StepName,
                                claimed.ClaimToken,
                                ex.Message,
                                cancellationToken);

                            trace.SetTag("failed", result);
                            trace.SetTag("workerId", Environment.MachineName);
                            trace.SetTag("claimToken", claimed.ClaimToken);
                            trace.SetTag("errorType", ex.GetType().FullName);

                            return result;
                        });

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        claimed.StepName);

                    _engineServices.Logger.Engine.StepException(
                        record.ExecutionId,
                        claimed.StepName,
                        ex);

                    var failedState = await _engineServices.DagStore.GetStateAsync(
                        executionId,
                        cancellationToken) ?? state;

                    var claimedStep = resolvedPipeline.Steps.First(
                        x => x.Name == claimed.StepName);

                    var executionContext = buildExecutionContext(
                        record,
                        failedState,
                        cancellationToken);

                    var stepContext = new AiStepExecutionContext(
                        executionContext,
                        claimedStep);

                    await _retentionCoordinator.ApplyRetentionPersistAndWarmAsync(
                        executionId,
                        failedState,
                        stepContext,
                        cancellationToken);

                    record.Steps = resolvedPipeline.Steps
                        .Select(x => x.Name)
                        .ToList();

                    var convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                        resolvedPipeline,
                        failedState,
                        _engineServices.StateWriter,
                        _engineServices.StepResolver,
                        DateTime.UtcNow,
                        cancellationToken);

                    AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                        record,
                        convergence,
                        failedState);

                    var expectedStepKey = record.ExecutionStepKey;

                    if (string.IsNullOrWhiteSpace(expectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await _finalizationService.PersistDistributedConvergedRecordAsync(
                        record,
                        convergence,
                        expectedStepKey,
                        failedState,
                        resolvedPipeline,
                        buildExecutionContext,
                        persistAsync,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        _engineServices.Logger.Engine.ExecutionCompleted(record);

                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(
                            record.ExecutionId);

                        await _lifecycleHelper.TryPersistTerminalSnapshotAsync(
                            record,
                            failedState,
                            cancellationToken);
                    }

                    await _lifecycleHelper.TryCleanupIfNeededAsync(
                        record,
                        cancellationToken);

                    throw;
                }

                if (!stepResult.Success)
                {
                    await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
                        new AiStorageTraceContext
                        {
                            ExecutionId = executionId,
                            StepId = claimed.StepName,
                            Backend = "Redis",
                            Operation = "TryFailStepResult"
                        },
                        async trace =>
                        {
                            var result = await _engineServices.DagStore.TryFailStepAsync(
                                executionId,
                                claimed.StepName,
                                claimed.ClaimToken,
                                stepResult.Error,
                                cancellationToken);

                            trace.SetTag("failed", result);
                            trace.SetTag("workerId", Environment.MachineName);
                            trace.SetTag("claimToken", claimed.ClaimToken);
                            trace.SetTag("error", stepResult.Error);

                            return result;
                        });

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        claimed.StepName);

                    _engineServices.Logger.Engine.StepFailed(
                        record.ExecutionId,
                        claimed.StepName,
                        stepResult.Error);

                    var failedState = await _engineServices.DagStore.GetStateAsync(
                        executionId,
                        cancellationToken) ?? state;

                    var failedStepState = failedState.Steps.TryGetValue(
                        claimed.StepName,
                        out var reloadedFailedStep)
                        ? reloadedFailedStep
                        : null;

                    if (failedStepState?.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        _engineServices.ObservabilityService.Metrics.Execution.RecordStepRetried(
                            executionId,
                            claimed.StepName);

                        _engineServices.Logger.Engine.StepRetryScheduled(
                            record.ExecutionId,
                            claimed.StepName,
                            failedStepState?.RetryState?.RetryCount ?? 0,
                            failedStepState?.RetryState?.NextRetryAtUtc);
                    }

                    state = failedState;
                }
                else
                {
                    var completed = await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
                        new AiStorageTraceContext
                        {
                            ExecutionId = executionId,
                            StepId = claimed.StepName,
                            Backend = "Redis",
                            Operation = "TryCompleteStep"
                        },
                        async trace =>
                        {
                            var result = await _engineServices.DagStore.TryCompleteStepAsync(
                                executionId,
                                claimed.StepName,
                                claimed.ClaimToken,
                                stepResult,
                                cancellationToken);

                            trace.SetTag("completed", result);
                            trace.SetTag("workerId", Environment.MachineName);
                            trace.SetTag("claimToken", claimed.ClaimToken);

                            return result;
                        });

                    if (!completed)
                    {
                        throw new InvalidOperationException(
                            $"Failed to complete claimed step '{claimed.StepName}' for execution '{executionId}'.");
                    }

                    _engineServices.Logger.Engine.StepCompleted(
                        record,
                        claimed.StepName);

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepCompleted(
                        executionId,
                        claimed.StepName);

                    var completedState = await _engineServices.DagStore.GetStateAsync(
                        executionId,
                        cancellationToken) ?? state;

                    var claimedStep = resolvedPipeline.Steps.First(
                        x => x.Name == claimed.StepName);

                    var executionContext = buildExecutionContext(
                        record,
                        completedState,
                        cancellationToken);

                    var stepContext = new AiStepExecutionContext(
                        executionContext,
                        claimedStep);

                    await _retentionCoordinator.ApplyRetentionPersistAndWarmAsync(
                        executionId,
                        completedState,
                        stepContext,
                        cancellationToken);

                    state = completedState;
                }

                var finalState = await _engineServices.DagStore.GetStateAsync(
                    executionId,
                    cancellationToken) ?? state;

                record.Steps = resolvedPipeline.Steps
                    .Select(x => x.Name)
                    .ToList();

                var finalConvergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                    resolvedPipeline,
                    finalState,
                    _engineServices.StateWriter,
                    _engineServices.StepResolver,
                    DateTime.UtcNow,
                    cancellationToken);

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI DAG] Distributed convergence evaluated. ExecutionId='{record.ExecutionId}', ConvergenceStatus='{finalConvergence.Status}'.");

                AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                    record,
                    finalConvergence,
                    finalState);

                if (record.Status == AiExecutionStatus.Failed)
                {
                    _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionFailed(
                        record.ExecutionId);
                }

                var expectedStepKeyFinal = record.ExecutionStepKey;

                if (string.IsNullOrWhiteSpace(expectedStepKeyFinal))
                {
                    throw new InvalidOperationException(
                        "ExecutionStepKey must be set before persisting execution state.");
                }

                await _finalizationService.PersistDistributedConvergedRecordAsync(
                    record,
                    finalConvergence,
                    expectedStepKeyFinal,
                    finalState,
                    resolvedPipeline,
                    buildExecutionContext,
                    persistAsync,
                    cancellationToken);

                if (record.IsTerminal)
                {
                    _engineServices.Logger.Engine.ExecutionCompleted(record);

                    _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(
                        record.ExecutionId);

                    await _lifecycleHelper.TryPersistTerminalSnapshotAsync(
                        record,
                        finalState,
                        cancellationToken);

                    await _lifecycleHelper.TryCleanupIfNeededAsync(
                        record,
                        cancellationToken);
                }

                return record;
            }
            finally
            {
                _engineServices.Accessor.Clear();
            }
        }
    }
}