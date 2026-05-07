using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Distributed;
using Multiplexed.AI.Runtime.Execution.Engine.Finalization;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;

namespace Multiplexed.AI.Runtime.Execution.Engine.Batch
{
    /// <summary>
    /// Executes bounded distributed DAG step batches.
    /// </summary>
    public sealed class AiDagBatchExecutionRunner
    {
        private readonly IAiDagExecutionEngineServices _engineServices;
        private readonly AiDagStepClaimService _claimService;
        private readonly AiDagClaimedStepExecutor _claimedStepExecutor;
        private readonly AiDagExecutionFinalizationService _finalizationService;
        private readonly AiDagExecutionLifecycleHelper _lifecycleHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagBatchExecutionRunner"/> class.
        /// </summary>
        public AiDagBatchExecutionRunner(
            IAiDagExecutionEngineServices engineServices,
            AiDagStepClaimService claimService,
            AiDagClaimedStepExecutor claimedStepExecutor,
            AiDagExecutionFinalizationService finalizationService,
            AiDagExecutionLifecycleHelper lifecycleHelper)
        {
            _engineServices = engineServices
                ?? throw new ArgumentNullException(nameof(engineServices));

            _claimService = claimService
                ?? throw new ArgumentNullException(nameof(claimService));

            _claimedStepExecutor = claimedStepExecutor
                ?? throw new ArgumentNullException(nameof(claimedStepExecutor));

            _finalizationService = finalizationService
                ?? throw new ArgumentNullException(nameof(finalizationService));

            _lifecycleHelper = lifecycleHelper
                ?? throw new ArgumentNullException(nameof(lifecycleHelper));
        }

        /// <summary>
        /// Executes a bounded distributed DAG step batch.
        /// </summary>
        public async Task<AiExecutionRecord> ExecuteBatchAsync(
            string executionId,
            int maxSteps,
            Func<string, Task> loadContextAndSetAsync,
            Func<AiExecutionRecord, AiExecutionState, CancellationToken, AiExecutionContext> buildExecutionContext,
            Func<AiExecutionRecord, string, AiExecutionState, CancellationToken, Task> persistAsync,
            Action<AiExecutionRecord> ensurePipelineName,
            Action<string> validateExecutionId,
            CancellationToken cancellationToken = default)
        {
            validateExecutionId(executionId);

            ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

            if (_engineServices.DagStore is null)
            {
                throw new InvalidOperationException(
                    "Distributed DAG store is not configured.");
            }

            var workerId = Guid.NewGuid().ToString("N");

            var record = await _engineServices.DagStore.GetRecordAsync(
                executionId,
                cancellationToken);

            if (record is null)
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' was not found.");
            }

            ensurePipelineName(record);

            await loadContextAndSetAsync(record.ContextKey);

            var resolvedPipeline = await _engineServices.PipelineExecutor.PrepareAsync(
                record.PipelineName!,
                cancellationToken);

            var state = await _engineServices.DagStore.GetStateAsync(
                executionId,
                cancellationToken);

            if (state is null)
            {
                throw new InvalidOperationException(
                    $"Execution state '{executionId}' was not found.");
            }

            var claimedSteps = await _claimService.ClaimBatchAsync(
                executionId,
                workerId,
                maxSteps,
                cancellationToken);

            if (claimedSteps.Count == 0)
            {
                var latestRecord = await _engineServices.DagStore.GetRecordAsync(
                    executionId,
                    cancellationToken);

                if (latestRecord?.IsTerminal == true)
                {
                    return latestRecord;
                }

                var latestState = await _engineServices.DagStore.GetStateAsync(
                    executionId,
                    cancellationToken) ?? state;

                record.Steps = resolvedPipeline.Steps
                    .Select(x => x.Name)
                    .ToList();

                var idleConvergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                    resolvedPipeline,
                    latestState,
                    _engineServices.StateWriter,
                    _engineServices.StepResolver,
                    DateTime.UtcNow,
                    cancellationToken);

                AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                    record,
                    idleConvergence,
                    latestState);

                if (record.IsTerminal)
                {
                    var idleExpectedStepKey = record.ExecutionStepKey;

                    if (string.IsNullOrWhiteSpace(idleExpectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await _finalizationService.PersistDistributedConvergedRecordAsync(
                        record,
                        idleConvergence,
                        idleExpectedStepKey,
                        latestState,
                        resolvedPipeline,
                        buildExecutionContext,
                        persistAsync,
                        cancellationToken);

                    return await _engineServices.DagStore.GetRecordAsync(
                               executionId,
                               cancellationToken)
                           ?? record;
                }

                return record;
            }

            var maxDegreeOfParallelism = ResolveMaxDegreeOfParallelism(
                resolvedPipeline,
                maxSteps);

            var batchResult =
                await _engineServices.StepExecutionOrchestrator.ExecuteAsync(
                    new AiDagStepExecutionContext
                    {
                        ExecutionId = executionId,
                        State = state,
                        MaxDegreeOfParallelism = maxDegreeOfParallelism
                    },
                    new AiStepExecutionBatch
                    {
                        Steps = claimedSteps
                    },
                    async (claimedStep, ct) =>
                    {
                        return await _claimedStepExecutor.ExecuteAsync(
                            record,
                            state,
                            resolvedPipeline,
                            claimedStep,
                            buildExecutionContext,
                            ct);
                    },
                    cancellationToken);

            foreach (var item in batchResult.Results)
            {
                var claimedStep = item.ClaimedStep;
                var result = item.Result;

                if (result.Success)
                {
                    var completed = await _engineServices.DagStore.TryCompleteStepAsync(
                        executionId,
                        claimedStep.StepName,
                        claimedStep.ClaimToken,
                        result,
                        cancellationToken);

                    if (!completed)
                    {
                        throw new InvalidOperationException(
                            $"Failed to complete claimed step '{claimedStep.StepName}' for execution '{executionId}'.");
                    }

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepCompleted(
                        executionId,
                        claimedStep.StepName);

                    _engineServices.Logger.Engine.LogInformation(
                        $"[AI DAG BATCH] Step completed. ExecutionId='{executionId}', StepName='{claimedStep.StepName}'.");
                }
                else
                {
                    await _engineServices.DagStore.TryFailStepAsync(
                        executionId,
                        claimedStep.StepName,
                        claimedStep.ClaimToken,
                        result.Error,
                        cancellationToken);

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        claimedStep.StepName);

                    _engineServices.Logger.Engine.LogInformation(
                        $"[AI DAG BATCH] Step failed. ExecutionId='{executionId}', StepName='{claimedStep.StepName}', Error='{result.Error}'.");
                }
            }

            var finalState = await _engineServices.DagStore.GetStateAsync(
                executionId,
                cancellationToken) ?? state;

            record.Steps = resolvedPipeline.Steps
                .Select(x => x.Name)
                .ToList();

            var convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                resolvedPipeline,
                finalState,
                _engineServices.StateWriter,
                _engineServices.StepResolver,
                DateTime.UtcNow,
                cancellationToken);

            AiDagExecutionRecordFinalizer.ApplyConvergenceToRecord(
                record,
                convergence,
                finalState);

            var expectedStepKey = AiDagExecutionHelpers.GetRequiredExecutionStepKey(record);

            await _finalizationService.PersistDistributedConvergedRecordAsync(
                record,
                convergence,
                expectedStepKey,
                finalState,
                resolvedPipeline,
                buildExecutionContext,
                persistAsync,
                cancellationToken);

            if (record.IsTerminal)
            {
                await _lifecycleHelper.TryPersistTerminalSnapshotAsync(
                    record,
                    finalState,
                    cancellationToken);

                await _lifecycleHelper.TryCleanupIfNeededAsync(
                    record,
                    cancellationToken);
            }

            return await _engineServices.DagStore.GetRecordAsync(
                       executionId,
                       cancellationToken)
                   ?? record;
        }

        /// <summary>
        /// Resolves the effective maximum degree of parallelism.
        /// </summary>
        private static int ResolveMaxDegreeOfParallelism(
            ResolvedAiPipeline resolvedPipeline,
            int requestedMaxSteps)
        {
            ArgumentNullException.ThrowIfNull(resolvedPipeline);

            var definition = resolvedPipeline.ParallelExecution;

            if (definition?.Enabled != true)
            {
                return 1;
            }

            var configuredMaxDegreeOfParallelism =
                definition.MaxDegreeOfParallelism <= 0
                    ? 1
                    : definition.MaxDegreeOfParallelism;

            return Math.Min(
                requestedMaxSteps,
                configuredMaxDegreeOfParallelism);
        }
    }
}