using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Finalization;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;

namespace Multiplexed.AI.Runtime.Execution.Engine.Batch
{
    /// <summary>
    /// Executes bounded distributed DAG step batches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runner coordinates distributed batch execution for DAG-based AI pipelines.
    /// It loads the execution record, resolves the pipeline, claims a bounded number of
    /// ready DAG steps, executes them with bounded local parallelism, persists their
    /// completion or failure transitions, and evaluates deterministic convergence.
    /// </para>
    ///
    /// <para>
    /// Distributed step claiming is concurrency-aware. Before a ready step is claimed,
    /// the claim service may acquire distributed concurrency capacity through the
    /// configured <see cref="IAiConcurrencyGate"/>.
    /// </para>
    ///
    /// <para>
    /// Because concurrency capacity is acquired before the actual DAG step execution,
    /// this runner is responsible for releasing the distributed concurrency lease after
    /// each claimed step has been completed or failed.
    /// </para>
    ///
    /// <para>
    /// The pipeline key used for throttling is intentionally stable across executions
    /// of the same pipeline. This allows multiple executions of the same pipeline to
    /// share the same distributed pipeline-level concurrency limit, while each concrete
    /// execution remains isolated through its own execution identifier.
    /// </para>
    /// </remarks>
    public sealed class AiDagBatchExecutionRunner
    {
        private static readonly IAiConcurrencyDefinitionResolver ConcurrencyDefinitionResolver =
            new DefaultAiConcurrencyDefinitionResolver();

        private readonly IAiDagExecutionEngineServices _engineServices;
        private readonly AiDagStepClaimService _claimService;
        private readonly AiDagClaimedStepExecutor _claimedStepExecutor;
        private readonly AiDagExecutionFinalizationService _finalizationService;
        private readonly AiDagExecutionLifecycleHelper _lifecycleHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagBatchExecutionRunner"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The composed DAG execution engine services used by the runner.
        /// </param>
        /// <param name="claimService">
        /// The distributed step claim service responsible for recovery, admission, and step claiming.
        /// </param>
        /// <param name="claimedStepExecutor">
        /// The executor responsible for executing already-claimed DAG steps.
        /// </param>
        /// <param name="finalizationService">
        /// The service responsible for persisting converged distributed execution records.
        /// </param>
        /// <param name="lifecycleHelper">
        /// The helper responsible for terminal snapshot persistence and cleanup operations.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when one of the required dependencies is <see langword="null"/>.
        /// </exception>
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
        /// <param name="executionId">
        /// The identifier of the distributed DAG execution to advance.
        /// </param>
        /// <param name="maxSteps">
        /// The maximum number of ready DAG steps to claim and execute in this batch.
        /// </param>
        /// <param name="loadContextAndSetAsync">
        /// A delegate used to load the execution context from the record context key and set it
        /// as the current runtime context.
        /// </param>
        /// <param name="buildExecutionContext">
        /// A delegate used to build an execution context from the execution record and state.
        /// </param>
        /// <param name="persistAsync">
        /// A delegate used to persist the execution record and state after convergence.
        /// </param>
        /// <param name="ensurePipelineName">
        /// A delegate used to validate or populate the execution record pipeline name.
        /// </param>
        /// <param name="validateExecutionId">
        /// A delegate used to validate the supplied execution identifier.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token used to cancel the batch execution operation.
        /// </param>
        /// <returns>
        /// The latest execution record after the batch execution and convergence evaluation.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method uses the following distributed execution sequence:
        /// </para>
        ///
        /// <list type="number">
        /// <item>
        /// <description>Load the execution record and execution state.</description>
        /// </item>
        /// <item>
        /// <description>Resolve the pipeline and derive a stable pipeline key.</description>
        /// </item>
        /// <item>
        /// <description>Recover timed-out steps and claim a bounded number of ready steps.</description>
        /// </item>
        /// <item>
        /// <description>Execute claimed steps with bounded local parallelism.</description>
        /// </item>
        /// <item>
        /// <description>Persist completion or failure transitions in the distributed DAG store.</description>
        /// </item>
        /// <item>
        /// <description>Release the distributed concurrency lease for each claimed step.</description>
        /// </item>
        /// <item>
        /// <description>Evaluate deterministic convergence and persist the converged record.</description>
        /// </item>
        /// </list>
        ///
        /// <para>
        /// The concurrency lease release happens in a <c>finally</c> block for each completed
        /// batch result. This prevents the runtime from keeping distributed capacity until TTL
        /// expiration when a completion or failure persistence operation throws.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the distributed DAG store is not configured, the execution record cannot be found,
        /// the execution state cannot be found, or a claimed step cannot be completed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="maxSteps"/> is less than one.
        /// </exception>
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

            if (resolvedPipeline is null)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{record.PipelineName}' could not be resolved for execution '{executionId}'.");
            }

            var pipelineKey = CreatePipelineKey(resolvedPipeline);

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
                resolvedPipeline,
                pipelineKey,
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
                state,
                claimedSteps,
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

                try
                {
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
                finally
                {
                    await ReleaseConcurrencyLeaseAsync(
                        executionId,
                        pipelineKey,
                        claimedStep.StepName,
                        workerId,
                        state,
                        resolvedPipeline,
                        CancellationToken.None).ConfigureAwait(false);
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
        /// Creates the stable pipeline key used by distributed concurrency scopes.
        /// </summary>
        /// <param name="pipeline">
        /// The resolved pipeline definition.
        /// </param>
        /// <returns>
        /// A stable pipeline key suitable for Redis concurrency scopes.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The pipeline key must be stable across multiple executions of the same pipeline.
        /// This is what allows <c>MaxPipelineConcurrency</c> to apply globally to the pipeline
        /// instead of being isolated per execution.
        /// </para>
        ///
        /// <para>
        /// When a version is available, the key uses <c>{Name}:{Version}</c>. Otherwise, it falls
        /// back to the pipeline name.
        /// </para>
        /// </remarks>
        private static string CreatePipelineKey(ResolvedAiPipeline pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            if (string.IsNullOrWhiteSpace(pipeline.Name))
            {
                throw new InvalidOperationException(
                    "Resolved pipeline name is required to build a stable concurrency pipeline key.");
            }

            return string.IsNullOrWhiteSpace(pipeline.Version)
                ? pipeline.Name
                : $"{pipeline.Name}:{pipeline.Version}";
        }

        /// <summary>
        /// Releases the distributed concurrency lease associated with a claimed step.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable pipeline key used during concurrency admission.
        /// </param>
        /// <param name="stepName">
        /// The claimed step name.
        /// </param>
        /// <param name="workerId">
        /// The worker identifier used during concurrency admission.
        /// </param>
        /// <param name="state">
        /// The execution state containing the step configuration required to resolve the same
        /// concurrency definition used during admission.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token for the release operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous release operation.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Release is best-effort from the runner perspective. The Redis gate implementation is
        /// crash-safe and TTL-based, so even if release cannot complete, the lease will eventually
        /// expire and be removed by a later acquisition attempt.
        /// </para>
        ///
        /// <para>
        /// This method intentionally reconstructs the same concurrency context and definition used
        /// during claim admission. That keeps the Redis scope keys and lease id aligned with the
        /// acquired lease.
        /// </para>
        /// </remarks>
        private async Task ReleaseConcurrencyLeaseAsync(
            string executionId,
            string pipelineKey,
            string stepName,
            string workerId,
            AiExecutionState state,
            ResolvedAiPipeline pipeline,
            CancellationToken cancellationToken)
        {
            if (!state.Steps.TryGetValue(stepName, out var stepState))
            {
                return;
            }

            var stepDefinition = FindPipelineStep(
                pipeline,
                stepName);

            var concurrencyAdmission = AiDagExecutionHelpers.CreateConcurrencyAdmission(
                executionId,
                pipelineKey,
                stepName,
                workerId,
                stepState,
                pipeline.Config,
                stepDefinition,
                ConcurrencyDefinitionResolver);

            if (!concurrencyAdmission.Definition.Enabled)
            {
                return;
            }

            await _engineServices.ConcurrencyGate.ReleaseAsync(
                concurrencyAdmission.Context,
                concurrencyAdmission.Definition,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds the pipeline step definition for a claimed step.
        /// </summary>
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
                    StepKey = string.IsNullOrWhiteSpace(step.StepKey)
                        ? step.Name
                        : step.StepKey,
                    Config = step.Config ?? new Dictionary<string, object?>()
                };
            }

            return new AiPipelineStepDefinition
            {
                Name = stepName,
                StepKey = stepName,
                Config = new Dictionary<string, object?>()
            };
        }

        /// <summary>
        /// Resolves the effective local maximum degree of parallelism from concurrency configuration.
        /// </summary>
        /// <param name="state">
        /// The current execution state.
        /// </param>
        /// <param name="claimedSteps">
        /// The claimed steps in the current batch.
        /// </param>
        /// <param name="requestedMaxSteps">
        /// The requested maximum number of steps.
        /// </param>
        /// <returns>
        /// The effective local maximum degree of parallelism for the current batch.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method resolves local bounded parallelism from the first claimed step configuration.
        /// Distributed concurrency limits are enforced earlier by the claim service and Redis gate.
        /// </para>
        ///
        /// <para>
        /// The returned value is always bounded by <paramref name="requestedMaxSteps"/>.
        /// </para>
        /// </remarks>
        private static int ResolveMaxDegreeOfParallelism(
            AiExecutionState state,
            IReadOnlyCollection<AiClaimedStep> claimedSteps,
            int requestedMaxSteps)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(claimedSteps);

            var firstClaimedStep = claimedSteps.FirstOrDefault();

            if (firstClaimedStep is null ||
                !state.Steps.TryGetValue(firstClaimedStep.StepName, out var stepState))
            {
                return 1;
            }

            var definition = ConcurrencyDefinitionResolver.Resolve(stepState);

            if (definition.MaxDegreeOfParallelism is not > 0)
            {
                return 1;
            }

            return Math.Min(
                requestedMaxSteps,
                definition.MaxDegreeOfParallelism.Value);
        }
    }
}