using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Executes AI pipelines using DAG-based scheduling.
    ///
    /// DAG behavior:
    /// - step readiness is determined from dependency satisfaction
    /// - per-step runtime status is the source of truth
    /// - CurrentStepIndex is not the orchestration driver
    ///
    /// LOCAL DAG behavior:
    /// - executes one ready step per call
    /// - uses in-memory / composite state evaluation
    /// - deterministic scheduling through dependency state and order
    ///
    /// DISTRIBUTED DAG behavior:
    /// - step claiming is delegated to <see cref="IAiDagExecutionStore"/>
    /// - readiness, retry delay, and dependency checks are enforced atomically by Redis Lua
    /// - completion/failure ownership is validated through claim tokens
    ///
    /// IMPORTANT:
    /// - This engine preserves the existing DAG execution flow for non-distributed stores
    /// - When a DAG store is provided, distributed claim/complete/fail semantics are used
    /// - RBAC context ownership still belongs to the execution as a whole, not to each step
    /// </summary>
    public sealed class AiDagExecutionEngine : AiExecutionEngine
    {
        private readonly IAiDagExecutionStore? _dagStore;
        private readonly IAiExecutionCleanupService _cleanupService;
        private readonly AiExecutionCleanupOptions _cleanupOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionEngine"/> class.
        /// </summary>
        public AiDagExecutionEngine(
            IAiExecutionStore store,
            IContextStore contextStore,
            IExecutionContextAccessor accessor,
            IExecutionContextFactory contextFactory,
            IServiceProvider services,
            IAiSequentialPipelineExecutor pipelineExecutor,
            IAiRuntimeLogger logger,
            IAiExecutionCleanupService cleanupService,
            IOptions<AiExecutionCleanupOptions> cleanupOptions,
            IAiDagExecutionStore? dagStore = null)
            : base(
                store,
                contextStore,
                accessor,
                contextFactory,
                services,
                pipelineExecutor,
                logger)
        {
            _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
            _cleanupOptions = cleanupOptions?.Value ?? throw new ArgumentNullException(nameof(cleanupOptions));
            _dagStore = dagStore;
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));
            }

            var current = Accessor.Current
                ?? throw new InvalidOperationException("No active RBAC context is available.");

            var preparedPipeline = await PipelineExecutor.PrepareAsync(
                pipelineName,
                cancellationToken);

            if (preparedPipeline.ExecutionMode != AiExecutionMode.Dag)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' is configured for mode '{preparedPipeline.ExecutionMode}' and cannot be created by the DAG engine.");
            }

            if (preparedPipeline.Steps.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' does not contain any resolved steps.");
            }

            // -----------------------------------------------------------------
            // Seed an AI-owned RBAC context copy.
            //
            // IMPORTANT:
            // - One execution owns one RBAC context copy
            // - DAG steps reuse the same context
            // - No per-step RBAC context rotation is performed here
            // -----------------------------------------------------------------

            var newContextKey = Guid.NewGuid().ToString("N");
            var aiOwnedContext = ContextFactory.CreateCopy(current, newContextKey);
            newContextKey = await ContextStore.SeedAsync(aiOwnedContext);

            var record = new AiExecutionRecord
            {
                PipelineName = pipelineName,
                ExecutionMode = preparedPipeline.ExecutionMode,
                ContextKey = newContextKey,
                Status = AiExecutionStatus.Pending,
                ExecutionContextSnapshot = ContextFactory.CreateSnapshot(current),
                Steps = preparedPipeline.Steps.Select(x => x.Name).ToList(),

                // Sequential compatibility fields only.
                CurrentStep = string.Empty,
                CurrentStepIndex = 0
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId,
                PipelineName = pipelineName
            };

            state.Set(AiExecutionKeys.Input, input);

            foreach (var step in preparedPipeline.Steps)
            {
                state.EnsureStepInitialized(step);

                // Keep DAG dependency metadata inside step state so the distributed
                // Redis DAG store can make claim decisions without re-resolving
                // the whole pipeline in Lua.
                var stepState = state.GetOrCreateStep(step.Name);
                stepState.DependsOn = step.DependsOn?.ToList() ?? new List<string>();
            }

            if (_dagStore is not null)
            {
                await _dagStore.CreateAsync(record, state, cancellationToken);
            }
            else
            {
                await Store.CreateAsync(record, state, cancellationToken);
            }

            Logger.Engine.ExecutionCreated(record);

            return record;
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return _dagStore is not null
                ? await ExecuteNextDistributedAsync(executionId, cancellationToken)
                : await ExecuteNextLocalAsync(executionId, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            AiExecutionRecord record;

            do
            {
                record = await ExecuteNextAsync(executionId, cancellationToken);

                // In DAG mode, Waiting means no further progress can be made
                // in the current execution loop.
                if (record.Status == AiExecutionStatus.Waiting)
                {
                    return record;
                }
            }
            while (!record.IsTerminal);

            return record;
        }

        // ---------------------------------------------------------------------
        // LOCAL / NON-DISTRIBUTED DAG EXECUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Executes one DAG step using the existing local orchestration model.
        ///
        /// This path is used when no distributed DAG store is configured.
        /// </summary>
        private async Task<AiExecutionRecord> ExecuteNextLocalAsync(
            string executionId,
            CancellationToken cancellationToken)
        {
            ValidateExecutionId(executionId);

            var (record, state) = await LoadExecutionAsync(
                executionId,
                cancellationToken);

            if (record.IsTerminal)
            {
                Logger.Engine.ExecutionAlreadyCompleted(record);

                await TryCleanupIfNeededAsync(record, cancellationToken);

                return record;
            }

            if (record.ExecutionMode != AiExecutionMode.Dag)
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' is configured for mode '{record.ExecutionMode}' and cannot be executed by the DAG engine.");
            }

            EnsurePipelineName(record);

            var expectedStepKey = record.ExecutionStepKey;
            var rbacContext = await LoadContextAsync(record.ContextKey);
            var executionContext = BuildExecutionContext(record, state, cancellationToken);

            var resolvedPipeline = await PipelineExecutor.PrepareAsync(
                record.PipelineName!,
                cancellationToken);

            Accessor.Set(rbacContext);

            try
            {
                var nextStep = AiPipelineDagStepSelector.SelectNextReadyStep(
                    resolvedPipeline,
                    state);

                if (nextStep is null)
                {
                    ApplyConvergenceToRecord(
                        record,
                        AiDagExecutionConvergenceEvaluator.Evaluate(resolvedPipeline, state),
                        state);

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    await PersistAsync(
                        record,
                        expectedStepKey,
                        state,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        Logger.Engine.ExecutionCompleted(record);
                        await TryCleanupIfNeededAsync(record, cancellationToken);
                    }

                    return record;
                }

                var stepState = state.GetOrCreateStep(nextStep.Name);

                // Local DAG path still marks running using synthetic local claim metadata.
                // This keeps the step lifecycle shape aligned with the distributed model.
                stepState.MarkRunning("local-worker", Guid.NewGuid().ToString("N"));

                record.MarkRunning();
                record.CurrentStep = nextStep.Name;

                var stepContext = new AiStepExecutionContext(
                    executionContext,
                    nextStep);

                var stepResult = await nextStep.Step.ExecuteAsync(
                    stepContext,
                    cancellationToken);

                if (!stepResult.Success)
                {
                    stepState.MarkFailed(stepResult.Error);

                    Logger.Engine.StepFailed(
                        record.ExecutionId,
                        nextStep.Name,
                        stepResult.Error);
                }
                else
                {
                    stepState.MarkCompleted(stepResult);

                    Logger.Engine.StepCompleted(
                        record,
                        nextStep.Name);
                }

                record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                ApplyConvergenceToRecord(
                    record,
                    AiDagExecutionConvergenceEvaluator.Evaluate(resolvedPipeline, state),
                    state);

                record.TouchVersion();
                record.RenewExecutionStepKey();

                await PersistAsync(
                    record,
                    expectedStepKey,
                    state,
                    cancellationToken);

                if (record.IsTerminal)
                {
                    Logger.Engine.ExecutionCompleted(record);
                    await TryCleanupIfNeededAsync(record, cancellationToken);
                }

                return record;
            }
            catch (Exception ex)
            {
                record.MarkFailed();
                record.TouchVersion();
                record.RenewExecutionStepKey();

                Logger.Engine.StepException(
                    record.ExecutionId,
                    record.CurrentStep,
                    ex);

                await Store.TryUpdateAsync(
                    record.ExecutionId,
                    expectedStepKey,
                    record,
                    state,
                    cancellationToken);

                await TryCleanupIfNeededAsync(record, cancellationToken);

                throw;
            }
            finally
            {
                Accessor.Clear();
            }
        }

        // ---------------------------------------------------------------------
        // DISTRIBUTED DAG EXECUTION (REDIS LUA)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Executes one DAG step using the distributed Redis/Lua claim model.
        ///
        /// Flow:
        /// - load execution record
        /// - recover timed-out running steps
        /// - claim one ready step atomically via Redis Lua
        /// - execute the claimed step
        /// - complete or fail the step using claim-token ownership
        /// - rebuild a fresh DAG snapshot
        /// - persist the global execution record through the existing record/state contract
        ///
        /// IMPORTANT:
        /// - step claim semantics come from the DAG store
        /// - pipeline completion is still derived from the full state snapshot
        /// - the execution record remains the global orchestration summary
        /// </summary>
        private async Task<AiExecutionRecord> ExecuteNextDistributedAsync(
            string executionId,
            CancellationToken cancellationToken)
        {
            ValidateExecutionId(executionId);

            var record = await _dagStore!.GetRecordAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException($"Execution '{executionId}' was not found.");

            if (record.IsTerminal)
            {
                Logger.Engine.ExecutionAlreadyCompleted(record);

                await TryCleanupIfNeededAsync(record, cancellationToken);

                return record;
            }

            if (record.ExecutionMode != AiExecutionMode.Dag)
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' is configured for mode '{record.ExecutionMode}' and cannot be executed by the DAG engine.");
            }

            EnsurePipelineName(record);

            var rbacContext = await LoadContextAsync(record.ContextKey);

            var resolvedPipeline = await PipelineExecutor.PrepareAsync(
                record.PipelineName!,
                cancellationToken);

            // Best-effort timed-out claim recovery before claiming new work.
            await _dagStore.RecoverTimedOutStepsAsync(executionId, cancellationToken);

            var claimed = await _dagStore.TryClaimNextReadyStepAsync(
                executionId,
                workerId: Environment.MachineName,
                cancellationToken: cancellationToken);

            // Reload a fresh state snapshot after recovery/claim attempt.
            var state = await _dagStore.GetStateAsync(executionId, cancellationToken)
                ?? new AiExecutionState
                {
                    ExecutionId = executionId,
                    PipelineName = record.PipelineName
                };

            Accessor.Set(rbacContext);

            try
            {
                // -------------------------------------------------------------
                // No step claim available.
                //
                // This means either:
                // - the DAG is completed
                // - the DAG is waiting on dependencies or retry timing
                // - the DAG still has in-flight work running on another worker
                // -------------------------------------------------------------
                if (claimed is null)
                {
                    record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                    ApplyConvergenceToRecord(
                        record,
                        AiDagExecutionConvergenceEvaluator.Evaluate(resolvedPipeline, state),
                        state);

                    var expectedStepKey = record.ExecutionStepKey;

                    await PersistDistributedConvergedRecordAsync(
                        record,
                        expectedStepKey,
                        state,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        Logger.Engine.ExecutionCompleted(record);
                        await TryCleanupIfNeededAsync(record, cancellationToken);
                    }

                    return record;
                }

                // -------------------------------------------------------------
                // Resolve claimed step from pipeline topology.
                // -------------------------------------------------------------
                var claimedStep = resolvedPipeline.Steps
                    .FirstOrDefault(x => string.Equals(x.Name, claimed.StepName, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        $"Claimed step '{claimed.StepName}' was not found in resolved pipeline '{resolvedPipeline.Name}'.");

                record.MarkRunning();
                record.CurrentStep = claimed.StepName;

                var executionContext = BuildExecutionContext(record, state, cancellationToken);

                var stepContext = new AiStepExecutionContext(
                    executionContext,
                    claimedStep);

                AiStepResult stepResult;

                try
                {
                    stepResult = await claimedStep.Step.ExecuteAsync(stepContext, cancellationToken);
                }
                catch (Exception ex)
                {
                    await _dagStore.TryFailStepAsync(
                        executionId,
                        claimed.StepName,
                        claimed.ClaimToken,
                        ex.Message,
                        cancellationToken);

                    Logger.Engine.StepException(
                        record.ExecutionId,
                        claimed.StepName,
                        ex);

                    var failedState = await _dagStore.GetStateAsync(executionId, cancellationToken) ?? state;

                    record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                    ApplyConvergenceToRecord(
                        record,
                        AiDagExecutionConvergenceEvaluator.Evaluate(resolvedPipeline, failedState),
                        failedState);

                    var expectedStepKey = record.ExecutionStepKey;

                    await PersistDistributedConvergedRecordAsync(
                        record,
                        expectedStepKey,
                        failedState,
                        cancellationToken);

                    await TryCleanupIfNeededAsync(record, cancellationToken);

                    throw;
                }

                if (!stepResult.Success)
                {
                    await _dagStore.TryFailStepAsync(
                        executionId,
                        claimed.StepName,
                        claimed.ClaimToken,
                        stepResult.Error,
                        cancellationToken);

                    Logger.Engine.StepFailed(
                        record.ExecutionId,
                        claimed.StepName,
                        stepResult.Error);
                }
                else
                {
                    var completed = await _dagStore.TryCompleteStepAsync(
                        executionId,
                        claimed.StepName,
                        claimed.ClaimToken,
                        stepResult,
                        cancellationToken);

                    if (!completed)
                    {
                        throw new InvalidOperationException(
                            $"Failed to complete claimed step '{claimed.StepName}' for execution '{executionId}'.");
                    }

                    Logger.Engine.StepCompleted(
                        record,
                        claimed.StepName);
                }

                // -------------------------------------------------------------
                // Reload final distributed state snapshot after completion/failure.
                // -------------------------------------------------------------
                var finalState = await _dagStore.GetStateAsync(executionId, cancellationToken) ?? state;

                record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                ApplyConvergenceToRecord(
                    record,
                    AiDagExecutionConvergenceEvaluator.Evaluate(resolvedPipeline, finalState),
                    finalState);

                var expectedStepKeyFinal = record.ExecutionStepKey;

                await PersistDistributedConvergedRecordAsync(
                    record,
                    expectedStepKeyFinal,
                    finalState,
                    cancellationToken);

                if (record.IsTerminal)
                {
                    Logger.Engine.ExecutionCompleted(record);
                    await TryCleanupIfNeededAsync(record, cancellationToken);
                }

                return record;
            }
            finally
            {
                Accessor.Clear();
            }
        }

        /// <summary>
        /// Applies a convergence decision to the global execution record.
        ///
        /// This method centralizes:
        /// - global status mutation
        /// - completed-step projection
        /// - current-step reset for converged snapshots
        /// </summary>
        private static void ApplyConvergenceToRecord(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(convergence);
            ArgumentNullException.ThrowIfNull(state);

            record.CompletedSteps = state.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            record.CurrentStep = string.Empty;

            switch (convergence.Status)
            {
                case AiExecutionStatus.Pending:
                    record.Status = AiExecutionStatus.Pending;
                    record.UpdatedAtUtc = DateTime.UtcNow;
                    break;

                case AiExecutionStatus.Running:
                    record.MarkRunning();
                    break;

                case AiExecutionStatus.Waiting:
                    record.MarkWaiting();
                    break;

                case AiExecutionStatus.Completed:
                    record.MarkCompleted();
                    break;

                case AiExecutionStatus.Failed:
                    record.MarkFailed();
                    break;

                case AiExecutionStatus.Cancelled:
                    record.MarkCancelled();
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported convergence status '{convergence.Status}'.");
            }
        }

        /// <summary>
        /// Attempts automatic cleanup only when configured and only when the execution is terminal.
        ///
        /// Cleanup is intentionally optional so completed or failed executions can still be
        /// inspected during development, testing, or debugging workflows.
        /// </summary>
        private async Task TryCleanupIfNeededAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (!record.IsTerminal)
            {
                Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' because the execution is not terminal.");

                return;
            }

            var shouldCleanup =
                (record.Status == AiExecutionStatus.Completed && _cleanupOptions.AutoCleanupOnCompleted) ||
                (record.Status == AiExecutionStatus.Failed && _cleanupOptions.AutoCleanupOnFailed);

            if (!shouldCleanup)
            {
                Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' with status '{record.Status}' because automatic cleanup is disabled.");

                return;
            }

            Logger.Engine.LogInformation(
                $"[AI CLEANUP] Starting for execution '{record.ExecutionId}' with status '{record.Status}'.");

            try
            {
                await _cleanupService.DeleteExecutionBundleAsync(
                    record.ExecutionId,
                    cancellationToken);

                Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Completed for execution '{record.ExecutionId}'.");
            }
            catch (Exception ex)
            {
                Logger.Engine.LogError(
                    ex,
                    $"[AI CLEANUP] Failed for execution '{record.ExecutionId}'.");

                if (!_cleanupOptions.SuppressCleanupExceptions)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Persists a converged execution record in a distributed-safe manner.
        ///
        /// This method ensures that terminal state transitions (<see cref="AiExecutionStatus.Completed"/> or
        /// <see cref="AiExecutionStatus.Failed"/>) are promoted atomically using the distributed DAG store,
        /// preventing race conditions across multiple workers.
        ///
        /// For non-terminal states, this method falls back to the standard persistence flow.
        ///
        /// BEHAVIOR:
        /// - If no DAG store is configured, falls back to <c>PersistAsync</c>
        /// - If terminal state, uses <c>TryFinalizeExecutionAsync</c>
        /// - If another worker wins the race, reloads the authoritative record
        /// - Ensures monotonic terminal state transitions
        ///
        /// GUARANTEES:
        /// - No double finalization
        /// - No state downgrade after terminal
        /// - Deterministic final state across distributed workers
        /// </summary>
        /// <param name="record">The execution record to persist.</param>
        /// <param name="expectedStepKey">The expected optimistic concurrency key.</param>
        /// <param name="state">The current execution state snapshot.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task PersistDistributedConvergedRecordAsync(
            AiExecutionRecord record,
            string expectedStepKey,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedStepKey);
            ArgumentNullException.ThrowIfNull(state);

            // ---------------------------------------------------------------------
            // Fallback for non-distributed execution
            // ---------------------------------------------------------------------
            if (_dagStore is null)
            {
                record.TouchVersion();
                record.RenewExecutionStepKey();

                await PersistAsync(
                    record,
                    expectedStepKey,
                    state,
                    cancellationToken);

                return;
            }

            var completedSteps = state.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            // ---------------------------------------------------------------------
            // Terminal state → atomic finalization
            // ---------------------------------------------------------------------
            if (record.Status is AiExecutionStatus.Completed or AiExecutionStatus.Failed)
            {
                var success = await _dagStore.TryFinalizeExecutionAsync(
                    new AiDagExecutionFinalizationRequest
                    {
                        ExecutionId = record.ExecutionId,
                        ExpectedExecutionStepKey = expectedStepKey,
                        Status = record.Status,
                        CompletedSteps = completedSteps,
                        CurrentStep = record.CurrentStep,
                        WorkerId = Environment.MachineName
                    },
                    cancellationToken);

                if (!success)
                {
                    // Another worker won the race → reload authoritative state
                    var refreshed = await _dagStore.GetRecordAsync(
                        record.ExecutionId,
                        cancellationToken);

                    if (refreshed is not null)
                    {
                        record.Status = refreshed.Status;
                        record.CompletedSteps = refreshed.CompletedSteps;
                        record.CurrentStep = refreshed.CurrentStep;
                        record.ExecutionStepKey = refreshed.ExecutionStepKey;
                        record.Version = refreshed.Version;
                        record.UpdatedAtUtc = refreshed.UpdatedAtUtc;
                    }

                    return;
                }

                // Reload final authoritative state
                var updated = await _dagStore.GetRecordAsync(
                    record.ExecutionId,
                    cancellationToken);

                if (updated is not null)
                {
                    record.Status = updated.Status;
                    record.CompletedSteps = updated.CompletedSteps;
                    record.CurrentStep = updated.CurrentStep;
                    record.ExecutionStepKey = updated.ExecutionStepKey;
                    record.Version = updated.Version;
                    record.UpdatedAtUtc = updated.UpdatedAtUtc;
                }

                return;
            }

            // ---------------------------------------------------------------------
            // Non-terminal → standard persistence
            // ---------------------------------------------------------------------
            record.TouchVersion();
            record.RenewExecutionStepKey();

            await PersistAsync(
                record,
                expectedStepKey,
                state,
                cancellationToken);
        }
    }
}