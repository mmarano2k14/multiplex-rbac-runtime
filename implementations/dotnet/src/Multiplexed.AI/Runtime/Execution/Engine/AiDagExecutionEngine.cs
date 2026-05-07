using Microsoft.Extensions.Options;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Linq;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Execution.Retention.Models;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution.Engine
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
    ///
    /// DISTRIBUTED EXECUTION MODEL:
    /// - The step-state snapshot is the source of truth
    /// - The execution record is only a projected orchestration summary
    /// - Terminal execution states must be finalized atomically
    /// - Multi-worker safety is enforced through the DAG store
    /// </summary>
    public sealed class AiDagExecutionEngine : AiExecutionEngine
    {

        private readonly IAiDagExecutionEngineServices _engineServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionEngine"/> class.
        /// </summary>
        public AiDagExecutionEngine(IAiDagExecutionEngineServices engineServices)
            : base(
                engineServices.Store,
                engineServices.ContextStore,
                engineServices.Accessor,
                engineServices.ContextFactory,
                engineServices.Services,
                engineServices.PipelineExecutor,
                engineServices.Logger,
                engineServices.StateReader,
                engineServices.StateWriter)
        {
            _engineServices = engineServices;
        }

        /// <inheritdoc />
        public override Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));
            }

            return CreateInternalAsync(
                pipelineName,
                state => _engineServices.StateWriter.SetData(state,AiExecutionKeys.Input, input),
                cancellationToken);
        }

        // <summary>
        /// Creates a new DAG execution using a structured state input payload.
        ///
        /// PURPOSE:
        /// - Supports modern pipeline inputs where multiple named values must be seeded
        ///   into the execution state
        /// - Keeps the existing string input overload backward compatible
        /// - Allows declarative JSON pipeline bindings such as:
        ///   state.cv, state.job, state.language, etc.
        ///
        /// IMPORTANT:
        /// - Input values are written directly into the execution state root
        /// - Existing pipelines using <see cref="AiExecutionKeys.Input"/> remain supported
        ///   through the string overload
        /// </summary>
        /// <param name="pipelineName">The pipeline name to execute.</param>
        /// <param name="input">The structured input values to seed into execution state.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created execution record.</returns>
        public override Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            IDictionary<string, object?> input,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentNullException.ThrowIfNull(input);

            return CreateInternalAsync(
                pipelineName,
                state =>
                {
                    foreach (var pair in input)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Key))
                        {
                            throw new ArgumentException(
                                "Structured input contains an empty or whitespace key.",
                                nameof(input));
                        }

                        _engineServices.StateWriter.SetData(state, pair.Key, pair.Value);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Shared DAG execution creation logic used by all public CreateAsync overloads.
        ///
        /// PURPOSE:
        /// - Centralizes record and state initialization
        /// - Avoids duplicating DAG creation logic across input overloads
        /// - Preserves existing behavior while allowing new input shapes
        ///
        /// IMPORTANT:
        /// - The provided <paramref name="seedState"/> callback is the only variable part
        ///   between overloads
        /// - RBAC context copy, pipeline preparation, and durable step initialization
        ///   remain identical for all create paths
        /// </summary>
        /// <param name="pipelineName">The pipeline name to execute.</param>
        /// <param name="seedState">
        /// Callback used to seed initial values into the execution state.
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created execution record.</returns>
        private async Task<AiExecutionRecord> CreateInternalAsync(
            string pipelineName,
            Action<AiExecutionState> seedState,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentNullException.ThrowIfNull(seedState);

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
                PipelineName = pipelineName,
                PipelineConfig = new Dictionary<string, object?>(
                    preparedPipeline.Config,
                    StringComparer.Ordinal)
            };

            // Seed runtime input into the execution state using the selected overload behavior.
            seedState(state);

            var executionContext = new AiExecutionContext(
                record,
                state,
                Services,
                _engineServices.StateReader,
                _engineServices.StateWriter,
                cancellationToken);

            foreach (var step in preparedPipeline.Steps)
            {

                _engineServices.StateWriter.EnsureStepInitialized(state, step);

                // Persist DAG dependency metadata directly in step state
                // so both local and distributed execution paths can evaluate readiness
                // without reintroducing global "current step" semantics.
                var stepState = _engineServices.StateWriter.GetOrCreateStep(state, step.Name);
                stepState.DependsOn = step.DependsOn?.ToList() ?? new List<string>();

                var stepContext = new AiStepExecutionContext(executionContext, step);

                var retryDefinition = await _engineServices.ObservabilityService.Tracer.TraceStepAsync(
                    new AiStepTraceContext
                    {
                        ExecutionId = record.ExecutionId,
                        StepId = step.Name,
                        StepType = "retry.defintion",
                        Status = "resolving.policy",
                        WorkerId = Environment.MachineName
                    },
                    async () =>
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        try
                        {
                            var definition = await _engineServices.PolicyEngineFactory
                                .Create<IAiRetryEngine>(AiPolicyKind.Retry, stepContext)
                                .ResolveRetryDefinitionAsync(cancellationToken);

                            sw.Stop();

                            _engineServices.ObservabilityService.Metrics.Policy.RecordExecution(
                                record.ExecutionId,
                                "retry.defintion",
                                success: true,
                                duration: sw.Elapsed
                            );

                            _engineServices.ObservabilityService.Metrics.Policy.RecordDecision(
                                record.ExecutionId,
                                "retry.defintion",
                                definition is null
                                    ? AiPolicyResultKind.Block
                                    : AiPolicyResultKind.Success
                            );

                            return definition;
                        }
                        catch
                        {
                            sw.Stop();

                            _engineServices.ObservabilityService.Metrics.Policy.RecordExecution(
                                record.ExecutionId,
                                "retry.defintion",
                                success: false,
                                duration: sw.Elapsed
                            );

                            _engineServices.ObservabilityService.Metrics.Policy.RecordFailure(
                                record.ExecutionId,
                                "retry.defintion"
                            );

                            throw;
                        }
                    });

                stepState.Retry = retryDefinition;
                stepState.RetryState ??= new AiStepRetryState();
            }

            if (_engineServices.DagStore is not null)
            {
                await _engineServices.DagStore.CreateAsync(record, state, cancellationToken);
            }
            else
            {
                await Store.CreateAsync(record, state, cancellationToken);
            }

            Logger.Engine.ExecutionCreated(record);

            _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionStarted(record.ExecutionId);


          
           Logger.Engine.LogInformation(
                $"[AI DAG] Execution created. ExecutionId='{record.ExecutionId}', Pipeline='{record.PipelineName}', Mode='{record.ExecutionMode}', StepCount='{preparedPipeline.Steps.Count}', ContextKey='{record.ContextKey}'.");

            return record;
        }



        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return _engineServices.DagStore is not null
                ? await ExecuteNextDistributedAsync(executionId, cancellationToken)
                : await ExecuteNextLocalAsync(executionId, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await _engineServices.ObservabilityService.Tracer.TraceExecutionAsync(
                new AiExecutionTraceContext
                {
                    ExecutionId = executionId,
                    ExecutionMode = "Dag",
                    Status = "Running",
                    WorkerId = Environment.MachineName
                },
                async () =>
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
                            Logger.Engine.LogInformation(
                                $"[AI DAG] ExecuteAll stopped in Waiting. ExecutionId='{record.ExecutionId}', Status='{record.Status}'.");

                            return record;
                        }

                    }
                    while (!record.IsTerminal);

                    Logger.Engine.LogInformation(
                        $"[AI DAG] ExecuteAll reached terminal state. ExecutionId='{record.ExecutionId}', Status='{record.Status}'.");

                    return record;
                });
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
        /// - Convergence is still evaluated through the shared DAG convergence evaluator
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
                    _engineServices.StateWriter,
                    utcNow);

                // -------------------------------------------------------------
                // No local step is ready.
                //
                // This can mean:
                // - the DAG is complete
                // - the DAG is globally failed
                // - execution is waiting for retry timing or dependency completion
                // -------------------------------------------------------------
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

                    Logger.Engine.LogInformation(
                        $"[AI DAG] No local runnable step. ExecutionId='{record.ExecutionId}', ConvergenceStatus='{convergence.Status}'.");

                    ApplyConvergenceToRecord(
                        record,
                        convergence,
                        state);

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    if (string.IsNullOrWhiteSpace(expectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await PersistAsync(
                        record,
                        expectedStepKey,
                        state,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        Logger.Engine.ExecutionCompleted(record);
                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);
                        await TryPersistTerminalSnapshotAsync(record, state, cancellationToken);
                        await TryCleanupIfNeededAsync(record, cancellationToken);
                    }

                    return record;
                }

                var stepState = _engineServices.StateWriter.GetOrCreateStep(state, nextStep.Name);

                // Synthetic local claim metadata is used only so local execution
                // keeps the same per-step lifecycle structure as distributed execution.
                stepState.MarkRunning("local-worker", Guid.NewGuid().ToString("N"));

                record.MarkRunning();
                record.CurrentStep = nextStep.Name;

                Logger.Engine.LogInformation(
                    $"[AI DAG] Local step started. ExecutionId='{record.ExecutionId}', StepName='{nextStep.Name}', Worker='local-worker'.");

                var stepContext = new AiStepExecutionContext(
                    executionContext,
                    nextStep);

                AiStepResult stepResult;

                try
                {
                    stepResult =
                     await _engineServices.ObservabilityService.Tracer.TraceStepAsync(
                         new AiStepTraceContext
                         {
                             ExecutionId = executionId,
                             StepId = nextStep.Name,
                             StepType = nextStep.Step.GetType().Name,
                             Status = "Running",
                             RetryCount = stepState.RetryState?.RetryCount ?? 0,
                             RecoveryCount = stepState.RecoveryCount,
                             WorkerId = Environment.MachineName,
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
                    // Retry-aware local failure:
                    // if retry budget remains, the step transitions to WaitingForRetry;
                    // otherwise it becomes terminally Failed.
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
                        _engineServices.ObservabilityService.Metrics.Execution.RecordStepRetried(executionId, nextStep.Name);

                        Logger.Engine.StepRetryScheduled(
                            record.ExecutionId,
                            nextStep.Name,
                            stepState.RetryState?.RetryCount ?? 0,
                            stepState.RetryState?.NextRetryAtUtc);
                    }

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        nextStep.Name);

                    Logger.Engine.StepException(
                        record.ExecutionId,
                        nextStep.Name,
                        ex);

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Local step exception applied. ExecutionId='{record.ExecutionId}', StepName='{nextStep.Name}', NewStatus='{stepState.Status}', RetryCount='{stepState.RetryState?.RetryCount ?? 0 }', NextRetryAtUtc='{stepState.RetryState?.NextRetryAtUtc?.ToString("u") ?? ""}'.");

                    record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                    convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                        resolvedPipeline,
                        state,
                        _engineServices.StateWriter,
                        _engineServices.StepResolver,
                        DateTime.UtcNow,
                        cancellationToken);

                    ApplyConvergenceToRecord(
                        record,
                        convergence,
                        state);

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    if (string.IsNullOrWhiteSpace(expectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await PersistAsync(
                        record,
                        expectedStepKey,
                        state,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        Logger.Engine.ExecutionCompleted(record);
                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);
                        await TryPersistTerminalSnapshotAsync(record, state, cancellationToken);
                        await TryCleanupIfNeededAsync(record, cancellationToken);
                    }

                    throw;
                }

                if (!stepResult.Success)
                {
                    // Retry-aware local unsuccessful result:
                    // business retry policy is applied through the step state itself.
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
                        _engineServices.ObservabilityService.Metrics.Execution.RecordStepRetried(executionId, nextStep.Name);

                        Logger.Engine.StepRetryScheduled(
                            record.ExecutionId,
                            nextStep.Name,
                            stepState.RetryState?.RetryCount ?? 0,
                            stepState.RetryState?.NextRetryAtUtc
                        );
                    }

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        nextStep.Name);

                    Logger.Engine.StepFailed(
                        record.ExecutionId,
                        nextStep.Name,
                        stepResult.Error);

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Local step retry/fail applied. ExecutionId='{record.ExecutionId}', StepName='{nextStep.Name}', NewStatus='{stepState.Status}', RetryCount='{stepState.RetryState?.RetryCount}', NextRetryAtUtc='{stepState.RetryState?.NextRetryAtUtc?.ToString("u")}'.");
                }
                else
                {
                    stepState.MarkCompleted(stepResult);

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepCompleted(
                        executionId,
                        nextStep.Name);

                    Logger.Engine.StepCompleted(
                        record,
                        nextStep.Name);

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Local step completed. ExecutionId='{record.ExecutionId}', StepName='{nextStep.Name}', DurationMs='{stepState.ElapsedDuration?.TotalMilliseconds}'.");
                }

                record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();


                convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                       resolvedPipeline,
                       state,
                       _engineServices.StateWriter,
                       _engineServices.StepResolver,
                       DateTime.UtcNow,
                       cancellationToken);

                ApplyConvergenceToRecord(
                    record,
                    convergence,
                    state);

                record.TouchVersion();
                record.RenewExecutionStepKey();

                if (string.IsNullOrWhiteSpace(expectedStepKey))
                {
                    throw new InvalidOperationException(
                        "ExecutionStepKey must be set before persisting execution state.");
                }

                await PersistAsync(
                    record,
                    expectedStepKey,
                    state,
                    cancellationToken);

                if (record.IsTerminal)
                {
                    Logger.Engine.ExecutionCompleted(record);
                    _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);
                    await TryPersistTerminalSnapshotAsync(record, state, cancellationToken);
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
        /// DISTRIBUTED EXECUTION INVARIANT:
        /// This method is safe to run concurrently across multiple workers.
        ///
        /// GUARANTEES:
        /// - At most one worker executes a given step claim at a time
        /// - Retry / timeout / recovery logic is delegated to the DAG store
        /// - Global convergence remains deterministic regardless of worker ordering
        /// - Terminal finalization remains atomic and monotonic
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
        /// - This method must remain side-effect safe outside of the store
        /// </summary>
        private async Task<AiExecutionRecord> ExecuteNextDistributedAsync(
            string executionId,
            CancellationToken cancellationToken)
        {
            ValidateExecutionId(executionId);

            var record = await _engineServices.DagStore!.GetRecordAsync(executionId, cancellationToken)
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
            var recoveredCount =
                await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
                    new AiStorageTraceContext
                    {
                        ExecutionId = executionId,
                        Backend = "Redis",
                        Operation = "RecoverTimedOutSteps"
                    },
                    async trace =>
                    {
                        var result = await _engineServices.DagStore.RecoverTimedOutStepsAsync(
                            executionId,
                            cancellationToken);

                        trace.SetTag("recoveredCount", result);
                        trace.SetTag("workerId", Environment.MachineName);
                        trace.SetTag("recovered", result > 0);

                        return result;
                    });

            if (recoveredCount > 0)
            {
                Logger.Engine.StepsRecovered(executionId, recoveredCount);

                Logger.Engine.LogInformation(
                    $"[AI DAG] Timed-out steps recovered. ExecutionId='{executionId}', RecoveredCount='{recoveredCount}'.");
            }

            var claimed =
                await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
                    new AiStorageTraceContext
                    {
                        ExecutionId = executionId,
                        Backend = "Redis",
                        Operation = "TryClaimNextReadyStep"
                    },
                    async trace =>
                    {
                        var result = await _engineServices.DagStore.TryClaimNextReadyStepAsync(
                            executionId,
                            workerId: Environment.MachineName,
                            cancellationToken: cancellationToken);

                        trace.SetTag("claimAcquired", result is not null);
                        trace.SetTag("workerId", Environment.MachineName);

                        if (result is not null)
                        {
                            trace.SetTag("stepId", result.StepName);
                            trace.SetTag("claimToken", result.ClaimToken);
                        }

                        return result;
                    });

            if (claimed is not null)
            {
                Logger.Engine.StepClaimed(
                    record.ExecutionId,
                    claimed.StepName,
                    Environment.MachineName,
                    claimed.ClaimToken);
            }

            // Reload fresh distributed state after recovery / claim attempt.
            var state = await _engineServices.DagStore.GetStateAsync(executionId, cancellationToken)
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

                    var convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                           resolvedPipeline,
                           state,
                           _engineServices.StateWriter,
                           _engineServices.StepResolver,
                           utcNow,
                           cancellationToken);

                    Logger.Engine.LogInformation(
                        $"[AI DAG] No distributed claim acquired. ExecutionId='{record.ExecutionId}', ConvergenceStatus='{convergence.Status}', Worker='{Environment.MachineName}'.");

                    ApplyConvergenceToRecord(
                        record,
                        convergence,
                        state);

                    var expectedStepKey = record.ExecutionStepKey;

                    if (string.IsNullOrWhiteSpace(expectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await PersistDistributedConvergedRecordAsync(
                        record,
                        convergence,
                        expectedStepKey,
                        state,
                        resolvedPipeline,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        Logger.Engine.ExecutionCompleted(record);
                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);
                        await TryPersistTerminalSnapshotAsync(record, state, cancellationToken);
                        await TryCleanupIfNeededAsync(record, cancellationToken);
                    }

                    return record;
                }

                Logger.Engine.LogInformation(
                    $"[AI DAG] Step claimed. ExecutionId='{record.ExecutionId}', StepName='{claimed.StepName}', Worker='{Environment.MachineName}', ClaimToken='{claimed.ClaimToken}'.");

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
                    stepResult =
                        await _engineServices.ObservabilityService.Tracer.TraceStepAsync(
                            new AiStepTraceContext
                            {
                                ExecutionId = executionId,
                                StepId = claimed.StepName,
                                StepType = claimedStep.Step.GetType().Name,
                                Status = "Running",
                                WorkerId = Environment.MachineName,
                                ClaimToken = claimed.ClaimToken
                            },
                            async () =>
                            {
                                var result = await claimedStep.Step.ExecuteAsync(
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
                    // Distributed failure path:
                    // step failure is persisted through the DAG store so ownership checks
                    // and retry scheduling remain atomic and multi-worker safe.

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

                    Logger.Engine.StepException(
                        record.ExecutionId,
                        claimed.StepName,
                        ex);

                    var failedState = await _engineServices.DagStore.GetStateAsync(executionId, cancellationToken) ?? state;

                    await ApplyRetentionPersistAndWarmAsync(
                        executionId,
                        failedState,
                        stepContext,
                        cancellationToken);

                    var failedStepState = failedState.Steps.TryGetValue(claimed.StepName, out var reloadedFailedStep)
                        ? reloadedFailedStep
                        : null;

                    if (failedStepState?.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        _engineServices.ObservabilityService.Metrics.Execution.RecordStepRetried(executionId, claimed.StepName);

                        Logger.Engine.StepRetryScheduled(
                            record.ExecutionId,
                            claimed.StepName,
                            failedStepState?.RetryState?.RetryCount ?? 0,
                            failedStepState?.RetryState?.NextRetryAtUtc);
                    }

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Distributed step exception applied. ExecutionId='{record.ExecutionId}', StepName='{claimed.StepName}', NewStatus='{failedStepState?.Status}', RetryCount='{failedStepState?.RetryState?.RetryCount}', RecoveryCount='{failedStepState?.RecoveryCount}', NextRetryAtUtc='{failedStepState?.RetryState?.NextRetryAtUtc?.ToString("u")}'.");

                    record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                    var convergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                          resolvedPipeline,
                          failedState,
                          _engineServices.StateWriter,
                          _engineServices.StepResolver,
                           DateTime.UtcNow,
                          cancellationToken);

                    ApplyConvergenceToRecord(
                        record,
                        convergence,
                        failedState);

                    var expectedStepKey = record.ExecutionStepKey;

                    if (string.IsNullOrWhiteSpace(expectedStepKey))
                    {
                        throw new InvalidOperationException(
                            "ExecutionStepKey must be set before persisting execution state.");
                    }

                    await PersistDistributedConvergedRecordAsync(
                        record,
                        convergence,
                        expectedStepKey,
                        failedState,
                        resolvedPipeline,
                        cancellationToken);

                    if (record.IsTerminal)
                    {
                        Logger.Engine.ExecutionCompleted(record);
                        _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);
                        await TryPersistTerminalSnapshotAsync(record, failedState, cancellationToken);
                    }

                    await TryCleanupIfNeededAsync(record, cancellationToken);

                    throw;
                }

                if (!stepResult.Success)
                {
                    // Distributed unsuccessful result:
                    // use the DAG store so retry scheduling and ownership validation
                    // remain centralized and atomic.
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


                    // Record the failure in metrics regardless of retry policy outcome.
                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepFailed(
                        executionId,
                        claimed.StepName);

                    Logger.Engine.StepFailed(
                        record.ExecutionId,
                        claimed.StepName,
                        stepResult.Error);

                    var failedState = await _engineServices.DagStore.GetStateAsync(executionId, cancellationToken) ?? state;
                    var failedStepState = failedState.Steps.TryGetValue(claimed.StepName, out var reloadedFailedStep)
                        ? reloadedFailedStep
                        : null;

                    if (failedStepState?.Status == AiStepExecutionStatus.WaitingForRetry)
                    {
                        _engineServices.ObservabilityService.Metrics.Execution.RecordStepRetried(executionId, claimed.StepName);

                        Logger.Engine.StepRetryScheduled(
                            record.ExecutionId,
                            claimed.StepName,
                            failedStepState?.RetryState?.RetryCount ?? 0,
                            failedStepState?.RetryState?.NextRetryAtUtc);
                    }

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Distributed step retry/fail applied. ExecutionId='{record.ExecutionId}', StepName='{claimed.StepName}', NewStatus='{failedStepState?.Status}', RetryCount='{failedStepState?.RetryState?.RetryCount}', RecoveryCount='{failedStepState?.RecoveryCount}', NextRetryAtUtc='{failedStepState?.RetryState?.NextRetryAtUtc?.ToString("u")}'.");

                    // Keep final authoritative state aligned with what was just persisted.
                    state = failedState;
                }
                else
                {
                    var completed =
                        await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
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

                    Logger.Engine.StepCompleted(
                        record,
                        claimed.StepName);

                    _engineServices.ObservabilityService.Metrics.Execution.RecordStepCompleted(
                        executionId,
                        claimed.StepName);

                    var completedState = await _engineServices.DagStore.GetStateAsync(executionId, cancellationToken) ?? state;

                    await ApplyRetentionPersistAndWarmAsync(
                        executionId,
                        completedState,
                        stepContext,
                        cancellationToken);


                    var completedStepState = completedState.Steps.TryGetValue(claimed.StepName, out var reloadedCompletedStep)
                        ? reloadedCompletedStep
                        : null;

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Distributed step completed. ExecutionId='{record.ExecutionId}', StepName='{claimed.StepName}', DurationMs='{completedStepState?.Duration?.TotalMilliseconds}'.");

                    // Keep final authoritative state aligned with what was just persisted.
                    state = completedState;
                }

                // Reload final authoritative distributed state snapshot
                // after completion / failure.
                var finalState = await _engineServices.DagStore.GetStateAsync(executionId, cancellationToken) ?? state;

                record.Steps = resolvedPipeline.Steps.Select(x => x.Name).ToList();

                var finalConvergence = await AiDagExecutionConvergenceEvaluator.EvaluateAsync(
                    resolvedPipeline,
                    finalState,
                    _engineServices.StateWriter,
                    _engineServices.StepResolver,
                    DateTime.UtcNow,
                    cancellationToken);

                Logger.Engine.LogInformation(
                    $"[AI DAG] Distributed convergence evaluated. ExecutionId='{record.ExecutionId}', ConvergenceStatus='{finalConvergence.Status}'.");

                ApplyConvergenceToRecord(
                    record,
                    finalConvergence,
                    finalState);

                if (record.Status == AiExecutionStatus.Failed)
                {
                    _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionFailed(record.ExecutionId);
                }

                var expectedStepKeyFinal = record.ExecutionStepKey;

                if (string.IsNullOrWhiteSpace(expectedStepKeyFinal))
                {
                    throw new InvalidOperationException(
                        "ExecutionStepKey must be set before persisting execution state.");
                }

                await PersistDistributedConvergedRecordAsync(
                    record,
                    finalConvergence,
                    expectedStepKeyFinal,
                    finalState,
                    resolvedPipeline,
                    cancellationToken);

                if (record.IsTerminal)
                {
                    Logger.Engine.ExecutionCompleted(record);
                    _engineServices.ObservabilityService.Metrics.Execution.RecordExecutionCompleted(record.ExecutionId);
                    await TryPersistTerminalSnapshotAsync(record, finalState, cancellationToken);
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
        ///
        /// IMPORTANT INVARIANT:
        /// The execution record must always reflect the evaluated step-state truth.
        /// Any divergence between record projection and authoritative step-state
        /// will break deterministic distributed convergence.
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
        /// Attempts to persist a durable execution snapshot for terminal executions.
        ///
        /// SNAPSHOT BEHAVIOR:
        /// - Snapshot persistence is optional and controlled through <see cref="AiEngineOptions"/>
        /// - Snapshot persistence is best-effort and must not interfere with execution flow
        /// - The current authoritative execution state is persisted only after terminal convergence
        ///
        /// IMPORTANT:
        /// - The distributed execution store remains the source of truth
        /// - The snapshot store is only used for audit, replay support, and post-mortem inspection
        /// - This method must be called before cleanup so the execution can still be inspected
        /// </summary>
        /// <param name="record">The terminal execution record.</param>
        /// <param name="state">The authoritative execution state snapshot.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task TryPersistTerminalSnapshotAsync(
         AiExecutionRecord record,
         AiExecutionState state,
         CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (!record.IsTerminal)
            {
                return;
            }

            if (!_engineServices.AiOptions.Value.Snapshots.Enabled)
            {
                return;
            }

            if (_engineServices.SnapshotService is null)
            {
                return;
            }

            try
            {
                ExecutionContextSnapshot? contextSnapshot = null;

                if (Accessor.Current is not null)
                {
                    contextSnapshot = ContextFactory.CreateSnapshot(Accessor.Current);
                }

                await _engineServices.SnapshotService.TryPersistAsync(
                    record,
                    state,
                    record.ContextKey,
                    contextSnapshot,
                    cancellationToken);

                Logger.Engine.SnapshotPersisted(record.ExecutionId, record.Status);

                Logger.Engine.LogInformation(
                    $"[AI SNAPSHOT] Persisted terminal snapshot for execution '{record.ExecutionId}' with status '{record.Status}'.");
            }
            catch (Exception ex)
            {
                Logger.Engine.LogError(
                    ex,
                    $"[AI SNAPSHOT] Failed for execution '{record.ExecutionId}'.");

                // Snapshot persistence is best-effort and must never interfere
                // with runtime finalization or cleanup behavior.
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
                Logger.Engine.CleanupSkipped(
                    record.ExecutionId,
                    "Execution is not terminal.");

                Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' because the execution is not terminal.");

                return;
            }

            var shouldCleanup =
                (record.Status == AiExecutionStatus.Completed && _engineServices.AiOptions.Value.Cleanup.AutoCleanupOnCompleted) ||
                (record.Status == AiExecutionStatus.Failed && _engineServices.AiOptions.Value.Cleanup.AutoCleanupOnFailed);

            if (!shouldCleanup)
            {
                Logger.Engine.CleanupSkipped(
                    record.ExecutionId,
                    "Automatic cleanup is disabled for the current terminal status.");

                Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' with status '{record.Status}' because automatic cleanup is disabled.");

                return;
            }

            Logger.Engine.CleanupStarted(record.ExecutionId, record.Status);

            Logger.Engine.LogInformation(
                $"[AI CLEANUP] Starting for execution '{record.ExecutionId}' with status '{record.Status}'.");

            try
            {
                await _engineServices.CleanupService.DeleteExecutionBundleAsync(
                    record.ExecutionId,
                    cancellationToken);

                Logger.Engine.CleanupCompleted(record.ExecutionId);

                Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Completed for execution '{record.ExecutionId}'.");
            }
            catch (Exception ex)
            {
                Logger.Engine.LogError(
                    ex,
                    $"[AI CLEANUP] Failed for execution '{record.ExecutionId}'.");

                if (!_engineServices.AiOptions.Value.Cleanup.SuppressCleanupExceptions)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Persists a converged execution record in a distributed-safe manner.
        ///
        /// TERMINAL BEHAVIOR:
        /// - Completed / Failed / Cancelled execution states are finalized atomically through the DAG store
        /// - If another worker wins the terminal race, the authoritative record is reloaded
        /// - Execution state retention is applied before finalization to ensure a compact terminal snapshot
        ///
        /// NON-TERMINAL BEHAVIOR:
        /// - Falls back to the standard optimistic persistence flow
        ///
        /// GUARANTEES:
        /// - No double finalization
        /// - No terminal-state downgrade
        /// - Deterministic final record projection across workers
        ///
        /// RETENTION:
        /// - Retention is applied only when convergence is terminal and finalization is allowed
        /// - Only completed steps are eligible for removal based on configured limits
        /// - Non-terminal steps (Running, Ready, WaitingForRetry, Failed) are never removed
        /// - Externalized payloads remain accessible via the payload store (Mongo/Redis)
        ///
        /// IMPORTANT:
        /// - The step-state snapshot remains the source of truth
        /// - The <paramref name="convergence"/> argument is the evaluated truth derived from that step-state
        /// - The execution record is only the projection being persisted
        /// - Retention must be applied before finalization to ensure consistent snapshot and persisted state
        /// </summary>
        /// <param name="record">The execution record projection to persist.</param>
        /// <param name="convergence">The evaluated convergence result derived from the current step-state snapshot.</param>
        /// <param name="expectedStepKey">The optimistic execution step key expected by the persistence layer.</param>
        /// <param name="state">The authoritative distributed step-state snapshot.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task PersistDistributedConvergedRecordAsync(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            string expectedStepKey,
            AiExecutionState state,
            ResolvedAiPipeline resolvedPipeline,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(convergence);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedStepKey);
            ArgumentNullException.ThrowIfNull(state);

            var utcNow = DateTime.UtcNow;

            // Fallback for local / non-distributed execution.
            if (_engineServices.DagStore is null)
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
            /*
            var completedSteps = state.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            */

            var completedSteps = record.CompletedSteps
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

            // ---------------------------------------------------------------------
            // TERMINAL FINALIZATION (DISTRIBUTED)
            //
            // This is the ONLY place where an execution is allowed to transition
            // into a terminal state in distributed mode.
            //
            // CRITICAL GUARANTEES:
            // - Multi-worker safe (Redis Lua atomicity)
            // - Idempotent (can be called multiple times safely)
            // - Monotonic (cannot downgrade terminal state)
            //
            // RETENTION INTEGRATION:
            // - Execution state retention is applied immediately before finalization
            // - Only completed steps may be evicted based on configured limits
            // - Non-terminal steps (Running, Ready, WaitingForRetry, Failed) are preserved
            // - Externalized payloads remain accessible via the payload store (Mongo/Redis)
            // - Ensures final state, snapshot, and persisted record remain compact
            //
            // IMPORTANT:
            // - Convergence MUST be evaluated BEFORE entering this block
            // - This method does NOT decide truth, it only persists it
            // - Step-state remains the single source of truth
            // - Retention must occur BEFORE finalization to ensure consistency
            //
            // FAILURE SCENARIO:
            // - If this worker loses the race, we MUST reload the authoritative record
            // ---------------------------------------------------------------------
            // if (convergence.IsTerminal && CanFinalize(state, convergence.Status))
            if (convergence.IsTerminal)
            {
                Logger.Engine.LogInformation(
                    $"[AI DAG] Finalization attempt. ExecutionId='{record.ExecutionId}', Status='{convergence.Status}', ExpectedStepKey='{expectedStepKey}'.");

                var retentionStep = resolvedPipeline.Steps.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Pipeline '{resolvedPipeline.Name}' does not contain any resolved step for retention context creation.");

                var executionContext = BuildExecutionContext(
                    record,
                    state,
                    cancellationToken);

                var retentionStepContext = new AiStepExecutionContext(
                    executionContext,
                    retentionStep);

                await ApplyRetentionPersistAndWarmAsync(
                    record.ExecutionId,
                    state,
                    retentionStepContext,
                    cancellationToken);

                var request = new AiDagExecutionFinalizationRequest
                {
                    ExecutionId = record.ExecutionId,
                    ExpectedExecutionStepKey = expectedStepKey,
                    Status = convergence.Status,
                    CompletedAtUtc = utcNow,
                    CompletedSteps = completedSteps,
                    CurrentStep = string.Empty,
                    WorkerId = Environment.MachineName
                };

                var success =
                    await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
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
                                cancellationToken);

                            trace.SetTag("finalized", result);
                            trace.SetTag("status", convergence.Status.ToString());
                            trace.SetTag("workerId", Environment.MachineName);
                            trace.SetTag("expectedStepKey", expectedStepKey);
                            trace.SetTag("completedSteps", completedSteps.Count);

                            return result;
                        });

                if (!success)
                {
                    Logger.Engine.FinalizationRaceLost(record.ExecutionId, convergence.Status);

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Finalization race lost. ExecutionId='{record.ExecutionId}', Status='{convergence.Status}'.");

                    // Another worker won the finalization race -> reload authoritative record.
                    var refreshed = await _engineServices.DagStore.GetRecordAsync(
                        record.ExecutionId,
                        cancellationToken);

                    if (refreshed is not null)
                    {
                        ApplyAuthoritativeRecord(record, refreshed);

                        Logger.Engine.LogInformation(
                            $"[AI DAG] Finalization authoritative record reloaded. ExecutionId='{record.ExecutionId}', Status='{record.Status}', Version='{record.Version}'.");
                    }

                    return;
                }

                Logger.Engine.FinalizationSucceeded(record.ExecutionId, convergence.Status);

                Logger.Engine.LogInformation(
                    $"[AI DAG] Finalization succeeded. ExecutionId='{record.ExecutionId}', Status='{convergence.Status}'.");

                // Reload final authoritative record after successful finalization.
                var updated = await _engineServices.DagStore.GetRecordAsync(
                    record.ExecutionId,
                    cancellationToken);

                if (updated is not null)
                {
                    ApplyAuthoritativeRecord(record, updated);

                    Logger.Engine.LogInformation(
                        $"[AI DAG] Final authoritative record loaded. ExecutionId='{record.ExecutionId}', Status='{record.Status}', Version='{record.Version}', CompletedSteps='{record.CompletedSteps.Count}'.");
                }

                return;
            }

            // ---------------------------------------------------------------------
            // NON-TERMINAL PERSISTENCE (DISTRIBUTED)
            //
            // IMPORTANT:
            // - In distributed mode, the step-state snapshot is already persisted
            //   through step-level claim / complete / fail / recovery operations
            // - We must NOT rewrite the full state here, otherwise we risk overwriting
            //   authoritative concurrent step mutations
            // - Only the execution record projection is updated here
            // ---------------------------------------------------------------------
            record.TouchVersion();
            record.RenewExecutionStepKey();

            await _engineServices.DagStore.SaveRecordAsync(record, cancellationToken);
        }

        /// <summary>
        /// Applies execution state retention, persists the updated state,
        /// and incrementally refreshes the step resolver cache.
        /// </summary>
        /// <remarks>
        /// PURPOSE:
        /// - Centralize the retention workflow into a single, safe operation.
        /// - Guarantee correct ordering of critical operations in distributed execution.
        /// - Ensure the resolver remains consistent with newly archived steps.
        ///
        /// ORDER OF OPERATIONS:
        /// 1. Apply retention through <see cref="IAiRetentionEngine"/>.
        /// 2. Persist the updated execution state.
        /// 3. Warm the step resolver for evicted steps.
        ///
        /// IMPORTANT:
        /// - The retention engine applies compaction before eviction.
        /// - The eviction service is responsible for removing evicted steps from hot state.
        /// - This method must not remove evicted steps a second time.
        /// </remarks>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="state">The current execution state.</param>
        /// <param name="stepContext">The current step execution context used to resolve config-driven policies.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ApplyRetentionPersistAndWarmAsync(
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

                    var dagStore = _engineServices.DagStore;

                    if (dagStore is not null)
                    {
                        await dagStore.SaveStateAsync(
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
                    trace.SetTag("workerId", Environment.MachineName);

                    return true;
                });
        }

        /// <summary>
        /// OBSOLETE:
        /// - This method was used before retention/eviction was introduced.
        /// - It only inspects hot state and is not compatible with archive-aware execution.
        /// - Finalization must now rely on convergence evaluation.
        /// </summary>
        [Obsolete(
        "CanFinalize is obsolete. Finalization is now driven by convergence evaluation (AiDagExecutionConvergenceEvaluator). " +
        "This method is not archive-aware and should not be used in retention-enabled execution paths.",
        false)]
        private static bool CanFinalize(
            AiExecutionState state,
            AiExecutionStatus targetStatus)
        {
            ArgumentNullException.ThrowIfNull(state);

            var steps = state.Steps.Values.ToList();

            // SAFETY: no steps means no valid terminal projection
            if (steps.Count == 0)
            {
                return false;
            }

            // ---------------------------------------------------------------------
            // SAFETY GUARD:
            // Do NOT allow finalization if any step is still active, retryable,
            // immediately runnable, or still ambiguous / uninitialized.
            // ---------------------------------------------------------------------
            if (steps.Any(x =>
                x.Status == AiStepExecutionStatus.Running ||
                x.Status == AiStepExecutionStatus.WaitingForRetry ||
                x.Status == AiStepExecutionStatus.Ready ||
                x.Status == AiStepExecutionStatus.None))
            {
                return false;
            }

            // ---------------------------------------------------------------------
            // COMPLETED:
            // All steps must be completed successfully.
            // ---------------------------------------------------------------------
            if (targetStatus == AiExecutionStatus.Completed)
            {
                return steps.All(x => x.IsCompleted);
            }

            // ---------------------------------------------------------------------
            // FAILED:
            // At least one failed step must exist and all remaining steps must already
            // be terminal as either Completed or Failed.
            // ---------------------------------------------------------------------
            if (targetStatus == AiExecutionStatus.Failed)
            {
                return steps.Any(x => x.Status == AiStepExecutionStatus.Failed)
                    && steps.All(x =>
                        x.Status == AiStepExecutionStatus.Failed ||
                        x.Status == AiStepExecutionStatus.Completed);
            }

            // ---------------------------------------------------------------------
            // CANCELLED:
            // Cancellation is treated as terminal only when all steps are already in
            // terminal states and no active or ambiguous work remains.
            // ---------------------------------------------------------------------
            if (targetStatus == AiExecutionStatus.Cancelled)
            {
                return steps.All(x =>
                    x.Status == AiStepExecutionStatus.Completed ||
                    x.Status == AiStepExecutionStatus.Failed);
            }

            return false;
        }

        /// <summary>
        /// Applies an authoritative persisted record snapshot onto the current
        /// in-memory execution record projection.
        ///
        /// PURPOSE:
        /// - Keep the caller's record instance aligned with the persisted truth
        /// - Reuse the same update logic after both successful finalization and race loss
        ///
        /// IMPORTANT:
        /// - The source record is assumed to be authoritative
        /// - This method does not merge state; it replaces the projected fields
        /// </summary>
        private static void ApplyAuthoritativeRecord(
            AiExecutionRecord target,
            AiExecutionRecord source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            target.Status = source.Status;
            target.CompletedSteps = source.CompletedSteps;
            target.CurrentStep = source.CurrentStep;
            target.ExecutionStepKey = source.ExecutionStepKey;
            target.Version = source.Version;
            target.UpdatedAtUtc = source.UpdatedAtUtc;
            target.CompletedAtUtc = source.CompletedAtUtc;
        }
    }
}