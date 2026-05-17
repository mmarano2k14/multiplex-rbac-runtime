using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Finalization;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;

namespace Multiplexed.AI.Runtime.Execution.Engine.Local
{
    /// <summary>
    /// Executes DAG pipelines using the local non-distributed execution path.
    /// </summary>
    public sealed class AiDagLocalExecutionRunner
    {
        private readonly IAiDagExecutionEngineServices _engineServices;
        private readonly AiDagExecutionLifecycleHelper _lifecycleHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagLocalExecutionRunner"/> class.
        /// </summary>
        /// <param name="engineServices">The DAG execution engine services.</param>
        /// <param name="lifecycleHelper">The terminal lifecycle helper.</param>
        public AiDagLocalExecutionRunner(
            IAiDagExecutionEngineServices engineServices,
            AiDagExecutionLifecycleHelper lifecycleHelper)
        {
            _engineServices = engineServices
                ?? throw new ArgumentNullException(nameof(engineServices));

            _lifecycleHelper = lifecycleHelper
                ?? throw new ArgumentNullException(nameof(lifecycleHelper));
        }

        /// <summary>
        /// Executes the next ready DAG step using the local execution path.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="loadExecutionAsync">Delegate used to load the execution record and state.</param>
        /// <param name="loadAndSetContextAsync">Delegate used to load and set the RBAC context.</param>
        /// <param name="buildExecutionContext">Delegate used to build an AI execution context.</param>
        /// <param name="persistAsync">Delegate used to persist the execution record and state.</param>
        /// <param name="ensurePipelineName">Delegate used to validate the pipeline name.</param>
        /// <param name="validateExecutionId">Delegate used to validate the execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated execution record.</returns>
        public async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            Func<string, CancellationToken, Task<(AiExecutionRecord Record, AiExecutionState State)>> loadExecutionAsync,
            Func<string, Task> loadAndSetContextAsync,
            Func<AiExecutionRecord, AiExecutionState, CancellationToken, AiExecutionContext> buildExecutionContext,
            Func<AiExecutionRecord, string, AiExecutionState, CancellationToken, Task> persistAsync,
            Action<AiExecutionRecord> ensurePipelineName,
            Action<string> validateExecutionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(loadExecutionAsync);
            ArgumentNullException.ThrowIfNull(loadAndSetContextAsync);
            ArgumentNullException.ThrowIfNull(buildExecutionContext);
            ArgumentNullException.ThrowIfNull(persistAsync);
            ArgumentNullException.ThrowIfNull(ensurePipelineName);
            ArgumentNullException.ThrowIfNull(validateExecutionId);

            validateExecutionId(executionId);

            var (record, state) = await loadExecutionAsync(
                executionId,
                cancellationToken);

            if (record.IsTerminal)
            {
                _engineServices.Logger.Engine.ExecutionAlreadyCompleted(record);

                await _lifecycleHelper.EnsureTerminalLifecycleAsync(
                    record,
                    state,
                    cancellationToken).ConfigureAwait(false);

                return record;
            }

            if (record.ExecutionMode != AiExecutionMode.Dag)
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' is configured for mode '{record.ExecutionMode}' and cannot be executed by the DAG engine.");
            }

            ensurePipelineName(record);

            var expectedStepKey = record.ExecutionStepKey;

            await loadAndSetContextAsync(record.ContextKey);

            var executionContext = buildExecutionContext(
                record,
                state,
                cancellationToken);

            var resolvedPipeline = await _engineServices.PipelineExecutor.PrepareAsync(
                record.PipelineName!,
                cancellationToken);

            try
            {
                var utcNow = DateTime.UtcNow;

                var nextStep = AiPipelineDagStepSelector.SelectNextReadyStep(
                    resolvedPipeline,
                    state,
                    _engineServices.StateWriter,
                    utcNow);

                AiDagExecutionConvergenceResult? convergence = null;

                if (nextStep is null)
                {
                    convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                        resolvedPipeline,
                        state,
                        _engineServices.StateWriter,
                        _engineServices.StepResolver,
                        DateTime.UtcNow,
                        cancellationToken);

                    _engineServices.Logger.Engine.LogInformation(
                        $"[AI DAG] No local runnable step. ExecutionId='{record.ExecutionId}', ConvergenceStatus='{convergence.Status}'.");

                    AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                        record,
                        convergence,
                        state,
                        AiDagExecutionHelpers.GetDeclaredStepNames(resolvedPipeline));

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    if (string.IsNullOrWhiteSpace(expectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await persistAsync(
                        record,
                        expectedStepKey,
                        state,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        _engineServices.Logger.Engine.ExecutionCompleted(record);
                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);

                        await _lifecycleHelper.EnsureTerminalLifecycleAsync(
                            record,
                            state,
                            cancellationToken).ConfigureAwait(false);
                    }

                    return record;
                }

                var stepState = _engineServices.StateWriter.GetOrCreateStep(
                    state,
                    nextStep.Name);

                var workerId = _engineServices.RuntimeInstanceIdentity.RuntimeInstanceId;
                var claimToken = Guid.NewGuid().ToString("N");

                stepState.MarkRunning(
                    workerId,
                    claimToken);

                record.MarkRunning();
                record.CurrentStep = nextStep.Name;

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI DAG] Local step started. ExecutionId='{record.ExecutionId}', StepName='{nextStep.Name}', Worker='{workerId}'.");

                var stepContext = new AiStepExecutionContext(
                    executionContext,
                    nextStep);

                AiStepResult stepResult;

                try
                {
                    stepResult = await _engineServices.ObservabilityService.Tracer.TraceStepAsync(
                        new AiStepTraceContext
                        {
                            ExecutionId = executionId,
                            StepId = nextStep.Name,
                            StepType = nextStep.Step.GetType().Name,
                            Status = "Running",
                            RetryCount = stepState.RetryState?.RetryCount ?? 0,
                            RecoveryCount = stepState.RecoveryCount,
                            WorkerId = workerId,
                            ClaimToken = stepState.ClaimToken
                        },
                        async () =>
                        {
                            var result = await nextStep.Step.ExecuteAsync(
                                stepContext,
                                cancellationToken);

                            await _engineServices.PayloadCompactor.CompactAsync(
                                result,
                                cancellationToken);

                            return result;
                        });
                }
                catch (Exception ex)
                {
                    await _engineServices.PolicyEngineFactory
                        .Create<IAiRetryEngine>(AiPolicyKind.Retry, stepContext)
                        .HandleFailureAsync(
                            stepState,
                            ex.Message,
                            ex,
                            DateTime.UtcNow,
                            cancellationToken);

                    if (stepState.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        _engineServices.ObservabilityService.Metrics.Execution.RecordStepRetried(
                            executionId,
                            nextStep.Name);

                        _engineServices.Logger.Engine.StepRetryScheduled(
                            record.ExecutionId,
                            nextStep.Name,
                            stepState.RetryState?.RetryCount ?? 0,
                            stepState.RetryState?.NextRetryAtUtc);
                    }

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        nextStep.Name);

                    _engineServices.Logger.Engine.StepException(
                        record.ExecutionId,
                        nextStep.Name,
                        ex);

                    record.Steps = resolvedPipeline.Steps
                        .Select(x => x.Name)
                        .ToList();

                    convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                        resolvedPipeline,
                        state,
                        _engineServices.StateWriter,
                        _engineServices.StepResolver,
                        DateTime.UtcNow,
                        cancellationToken);

                    AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                        record,
                        convergence,
                        state,
                        AiDagExecutionHelpers.GetDeclaredStepNames(resolvedPipeline));

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    if (string.IsNullOrWhiteSpace(expectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await persistAsync(
                        record,
                        expectedStepKey,
                        state,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        _engineServices.Logger.Engine.ExecutionCompleted(record);
                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);

                        await _lifecycleHelper.EnsureTerminalLifecycleAsync(
                            record,
                            state,
                            cancellationToken).ConfigureAwait(false);
                    }

                    throw;
                }

                if (!stepResult.Success)
                {
                    await _engineServices.PolicyEngineFactory
                        .Create<IAiRetryEngine>(AiPolicyKind.Retry, stepContext)
                        .HandleFailureAsync(
                            stepState,
                            stepResult.Error,
                            null,
                            DateTime.UtcNow,
                            cancellationToken);

                    if (stepState.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        _engineServices.ObservabilityService.Metrics.Execution.RecordStepRetried(
                            executionId,
                            nextStep.Name);

                        _engineServices.Logger.Engine.StepRetryScheduled(
                            record.ExecutionId,
                            nextStep.Name,
                            stepState.RetryState?.RetryCount ?? 0,
                            stepState.RetryState?.NextRetryAtUtc);
                    }

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        nextStep.Name);

                    _engineServices.Logger.Engine.StepFailed(
                        record.ExecutionId,
                        nextStep.Name,
                        stepResult.Error);
                }
                else
                {
                    stepState.MarkCompleted(stepResult);

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepCompleted(
                        executionId,
                        nextStep.Name);

                    _engineServices.Logger.Engine.StepCompleted(
                        record,
                        nextStep.Name);

                    _engineServices.Logger.Engine.LogInformation(
                        $"[AI DAG] Local step completed. ExecutionId='{record.ExecutionId}', StepName='{nextStep.Name}', DurationMs='{stepState.ElapsedDuration?.TotalMilliseconds}'.");
                }

                record.Steps = resolvedPipeline.Steps
                    .Select(x => x.Name)
                    .ToList();

                convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                    resolvedPipeline,
                    state,
                    _engineServices.StateWriter,
                    _engineServices.StepResolver,
                    DateTime.UtcNow,
                    cancellationToken);

                AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                    record,
                    convergence,
                    state,
                    AiDagExecutionHelpers.GetDeclaredStepNames(resolvedPipeline));

                record.TouchVersion();
                record.RenewExecutionStepKey();

                if (string.IsNullOrWhiteSpace(expectedStepKey))
                {
                    throw new InvalidOperationException(
                        "ExecutionStepKey must be set before persisting execution state.");
                }

                await persistAsync(
                    record,
                    expectedStepKey,
                    state,
                    cancellationToken);

                if (record.IsTerminal)
                {
                    _engineServices.Logger.Engine.ExecutionCompleted(record);
                    _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);

                    await _lifecycleHelper.EnsureTerminalLifecycleAsync(
                        record,
                        state,
                        cancellationToken).ConfigureAwait(false);
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