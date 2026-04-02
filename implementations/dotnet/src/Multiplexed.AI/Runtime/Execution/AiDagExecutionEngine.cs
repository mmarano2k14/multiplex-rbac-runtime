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
    /// Executes AI pipelines using DAG-based orchestration.
    ///
    /// DESIGN OVERVIEW:
    /// - This engine executes one DAG step at a time
    /// - Step readiness comes from per-step runtime state and dependency completion
    /// - Sequential fields on the execution record are preserved only for compatibility / observability
    ///
    /// EXECUTION MODES:
    /// - Local mode:
    ///   Uses the in-memory / store-backed state model directly
    /// - Distributed mode:
    ///   Delegates claim / complete / fail / recovery semantics to the DAG execution store
    ///
    /// IMPORTANT:
    /// - Global execution lifecycle is still represented by <see cref="AiExecutionRecord"/>
    /// - Per-step lifecycle is represented by <see cref="AiStepState"/>
    /// - RBAC context ownership belongs to the execution as a whole, not to individual steps
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
            // Seed one AI-owned RBAC context copy for the whole execution.
            //
            // IMPORTANT:
            // - One execution owns one RBAC context copy
            // - All DAG steps reuse the same context
            // - No per-step context cloning or rotation occurs here
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

                // Persist DAG dependency metadata directly in step state
                // so both local and distributed execution paths can evaluate readiness
                // without reintroducing global "current step" semantics.
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

                // In DAG mode, Waiting means:
                // - no runnable work exists right now
                // - execution is not terminal
                // - no further local progress can currently be made in this loop
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
        /// Executes one DAG step using the local / non-distributed execution path.
        ///
        /// BEHAVIOR:
        /// - Loads record and state
        /// - Resolves the pipeline
        /// - Selects the next ready step locally
        /// - Executes that step
        /// - Applies retry-aware transitions through <see cref="AiStepState"/>
        /// - Recomputes global convergence
        /// - Persists the updated execution snapshot
        ///
        /// IMPORTANT:
        /// - This path does not use distributed claims
        /// - Synthetic local claim metadata is still used to keep step lifecycle shape aligned
        ///   with the distributed model
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
                var utcNow = DateTime.UtcNow;

                var nextStep = AiPipelineDagStepSelector.SelectNextReadyStep(
                    resolvedPipeline,
                    state,
                    utcNow);

                // -------------------------------------------------------------
                // No local step is ready.
                //
                // This can mean:
                // - the DAG is complete
                // - the DAG is globally failed
                // - execution is waiting for retry timing or dependency completion
                // -------------------------------------------------------------
                if (nextStep is null)
                {
                    ApplyConvergenceToRecord(
                        record,
                        AiDagExecutionConvergenceEvaluator.Evaluate(resolvedPipeline, state, utcNow),
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

                // Synthetic local claim metadata is used only so local execution
                // keeps the same per-step lifecycle structure as distributed execution.
                stepState.MarkRunning("local-worker", Guid.NewGuid().ToString("N"));

                record.MarkRunning();
                record.CurrentStep = nextStep.Name;

                var stepContext = new AiStepExecutionContext(
                    executionContext,
                    nextStep);

                AiStepResult stepResult;

                try
                {
                    stepResult = await nextStep.Step.ExecuteAsync(
                        stepContext,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    // Retry-aware local failure:
                    // if retry budget remains, the step transitions to WaitingForRetry;
                    // otherwise it becomes terminally Failed.
                    stepState.MarkRetryOrFail(ex.Message, DateTime.UtcNow);

                    Logger.Engine.StepException(
                        record.ExecutionId,
                        nextStep.Name,
                        ex);

                    record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                    ApplyConvergenceToRecord(
                        record,
                        AiDagExecutionConvergenceEvaluator.Evaluate(
                            resolvedPipeline,
                            state,
                            DateTime.UtcNow),
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

                    throw;
                }

                if (!stepResult.Success)
                {
                    // Retry-aware local unsuccessful result:
                    // business retry policy is applied through the step state itself.
                    stepState.MarkRetryOrFail(stepResult.Error, DateTime.UtcNow);

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
                    AiDagExecutionConvergenceEvaluator.Evaluate(
                        resolvedPipeline,
                        state,
                        DateTime.UtcNow),
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
            finally
            {
                Accessor.Clear();
            }
        }

        // ---------------------------------------------------------------------
        // DISTRIBUTED DAG EXECUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Executes one DAG step using the distributed execution path.
        ///
        /// FLOW:
        /// - Load the execution record
        /// - Resolve the pipeline
        /// - Recover timed-out running steps through the DAG store
        /// - Atomically claim one ready step via the distributed store
        /// - Execute the claimed step
        /// - Complete or fail the step using claim-token ownership validation
        /// - Reload the authoritative distributed state snapshot
        /// - Recompute global convergence and persist the execution record
        ///
        /// IMPORTANT:
        /// - Claim / retry / timeout logic belongs to the DAG store and its Redis/Lua layer
        /// - The execution record remains the global orchestration summary
        /// - The step state remains the source of truth for DAG lifecycle
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

            // Best-effort timeout recovery before any new distributed claim attempt.
            await _dagStore.RecoverTimedOutStepsAsync(executionId, cancellationToken);

            var claimed = await _dagStore.TryClaimNextReadyStepAsync(
                executionId,
                workerId: Environment.MachineName,
                cancellationToken: cancellationToken);

            // Reload fresh distributed state after recovery / claim attempt.
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
                // No step was claimed.
                //
                // This can mean:
                // - the DAG is complete
                // - the DAG is waiting on dependency or retry timing
                // - another worker still owns the runnable work
                // -------------------------------------------------------------
                if (claimed is null)
                {
                    var utcNow = DateTime.UtcNow;

                    record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                    ApplyConvergenceToRecord(
                        record,
                        AiDagExecutionConvergenceEvaluator.Evaluate(resolvedPipeline, state, utcNow),
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

                // Resolve the claimed step against the resolved pipeline topology.
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
                    // Distributed failure path:
                    // step failure is persisted through the DAG store so ownership checks
                    // and retry scheduling remain atomic and multi-worker safe.
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
                        AiDagExecutionConvergenceEvaluator.Evaluate(
                            resolvedPipeline,
                            failedState,
                            DateTime.UtcNow),
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
                    // Distributed unsuccessful result:
                    // use the DAG store so retry scheduling and ownership validation
                    // remain centralized and atomic.
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

                // Reload final authoritative distributed state snapshot
                // after completion / failure.
                var finalState = await _dagStore.GetStateAsync(executionId, cancellationToken) ?? state;

                record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                ApplyConvergenceToRecord(
                    record,
                    AiDagExecutionConvergenceEvaluator.Evaluate(
                        resolvedPipeline,
                        finalState,
                        DateTime.UtcNow),
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
        /// RESPONSIBILITIES:
        /// - Project completed steps from the step-state snapshot
        /// - Reset the current step for converged snapshots
        /// - Mutate the global execution status accordingly
        ///
        /// IMPORTANT:
        /// The execution record is only a summary projection.
        /// The per-step state remains the source of truth for DAG lifecycle.
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
        /// Cleanup remains optional so completed / failed executions can still be inspected
        /// during development, testing, or debugging workflows.
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
        /// TERMINAL BEHAVIOR:
        /// - Completed / Failed execution states are finalized atomically through the DAG store
        /// - If another worker wins the terminal race, the authoritative record is reloaded
        ///
        /// NON-TERMINAL BEHAVIOR:
        /// - Falls back to the standard optimistic persistence flow
        ///
        /// GUARANTEES:
        /// - No double finalization
        /// - No terminal-state downgrade
        /// - Deterministic final record projection across workers
        /// </summary>
        private async Task PersistDistributedConvergedRecordAsync(
            AiExecutionRecord record,
            string expectedStepKey,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedStepKey);
            ArgumentNullException.ThrowIfNull(state);

            // Fallback for local / non-distributed execution.
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

            // Terminal execution states are finalized atomically in the distributed store.
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
                    // Another worker won the finalization race -> reload authoritative record.
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

                // Reload final authoritative record after successful finalization.
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

            // Non-terminal state -> standard optimistic persistence.
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