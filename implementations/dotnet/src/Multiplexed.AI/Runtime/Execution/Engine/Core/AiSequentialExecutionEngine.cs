using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution.Engine.Core
{
    /// <summary>
    /// Executes AI pipelines using strict sequential orchestration.
    ///
    /// Sequential behavior:
    /// - one current step at a time
    /// - progression driven by CurrentStepIndex
    /// - RBAC context rotation after each successful step
    ///
    /// This engine is the legacy-compatible sequential implementation.
    /// </summary>
    public sealed class AiSequentialExecutionEngine : AiExecutionEngine
    {
        private static readonly TimeSpan ContextRotationOverlap = TimeSpan.FromSeconds(30);

        private readonly IAiExecutionCleanupService _cleanupService;
        private readonly AiExecutionCleanupOptions _cleanupOptions;

        private readonly IAiExecutionStateReader _stateReader;
        private readonly IAiExecutionStateWriter _stateWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiSequentialExecutionEngine"/> class.
        /// </summary>
        public AiSequentialExecutionEngine(
            IAiExecutionStore store,
            IContextStore contextStore,
            IExecutionContextAccessor accessor,
            IExecutionContextFactory contextFactory,
            IServiceProvider services,
            IAiSequentialPipelineExecutor pipelineExecutor,
            IAiRuntimeLogger logger,
            IAiExecutionCleanupService cleanupService,
            IOptions<AiExecutionCleanupOptions> cleanupOptions,
            IAiExecutionStateReader stateReader,
            IAiExecutionStateWriter stateWriter)
            : base(
                store,
                contextStore,
                accessor,
                contextFactory,
                services,
                pipelineExecutor,
                logger, stateReader, stateWriter)
        {
            _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
            _cleanupOptions = cleanupOptions?.Value ?? throw new ArgumentNullException(nameof(cleanupOptions));
            _stateReader = stateReader ?? throw new ArgumentNullException(nameof(stateReader));
            _stateWriter = stateWriter ?? throw new ArgumentNullException(nameof(stateWriter));
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
                state => _stateWriter.SetData(state, AiExecutionKeys.Input, input),
                cancellationToken);
        }

        /// <summary>
        /// Creates a new sequential execution using a structured input payload.
        ///
        /// PURPOSE:
        /// - Supports modern pipeline inputs where multiple named values must be seeded
        ///   into the execution state
        /// - Preserves backward compatibility with the string input overload
        /// - Allows declarative bindings such as:
        ///   state.cv, state.job, state.language, etc.
        ///
        /// IMPORTANT:
        /// - Values are written directly into the execution state root
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

                        _stateWriter.SetData(state, pair.Key, pair.Value);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Shared sequential execution creation logic used by all public CreateAsync overloads.
        ///
        /// PURPOSE:
        /// - Centralizes record and state initialization
        /// - Avoids duplicating sequential creation logic across input overloads
        /// - Preserves existing behavior while allowing new input shapes
        ///
        /// IMPORTANT:
        /// - The provided <paramref name="seedState"/> callback is the only variable part
        ///   between overloads
        /// - RBAC context copy, pipeline preparation, and durable state initialization
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

            if (preparedPipeline.ExecutionMode != AiExecutionMode.Sequential)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' is configured for mode '{preparedPipeline.ExecutionMode}' and cannot be created by the sequential engine.");
            }

            var orderedSteps = preparedPipeline.Steps
                .OrderBy(x => x.Order)
                .ToArray();

            if (orderedSteps.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' does not contain any resolved steps.");
            }

            var newContextKey = Guid.NewGuid().ToString("N");
            var aiOwnedContext = ContextFactory.CreateCopy(current, newContextKey);
            newContextKey = await ContextStore.SeedAsync(aiOwnedContext);

            var record = new AiExecutionRecord
            {
                PipelineName = pipelineName,
                ExecutionMode = preparedPipeline.ExecutionMode,
                ContextKey = newContextKey,
                CurrentStep = orderedSteps[0].Name,
                CurrentStepIndex = 0,
                Status = AiExecutionStatus.Pending,
                ExecutionContextSnapshot = ContextFactory.CreateSnapshot(current),
                Steps = orderedSteps.Select(x => x.Name).ToList()
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId,
                PipelineName = pipelineName
            };

            seedState(state);

            await Store.CreateAsync(record, state, cancellationToken);

            Logger.Engine.ExecutionCreated(record);

            return record;
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default)
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

            if (record.ExecutionMode != AiExecutionMode.Sequential)
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' is configured for mode '{record.ExecutionMode}' and cannot be executed by the sequential engine.");
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
                var pipelineResult = await PipelineExecutor.ExecuteNextAsync(
                    resolvedPipeline,
                    executionContext,
                    cancellationToken);

                if (!pipelineResult.StepResult.Success)
                {
                    record.MarkFailed();

                    Logger.Engine.StepFailed(
                        record.ExecutionId,
                        record.CurrentStep ?? "unknown",
                        pipelineResult.StepResult.Error);

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    await PersistAsync(
                        record,
                        expectedStepKey ?? record.ExecutionStepKey ?? string.Empty,
                        state,
                        cancellationToken);

                    await TryCleanupIfNeededAsync(record, cancellationToken);

                    return record;
                }

                if (!string.IsNullOrWhiteSpace(pipelineResult.ExecutedStepName) &&
                    !record.CompletedSteps.Contains(pipelineResult.ExecutedStepName, StringComparer.Ordinal))
                {
                    record.CompletedSteps.Add(pipelineResult.ExecutedStepName);
                }

                record.Steps = pipelineResult.Steps.ToList();

                await RotateContextAsync(record, cancellationToken);
                MoveNext(record, pipelineResult);

                record.TouchVersion();
                record.RenewExecutionStepKey();

                await PersistAsync(
                    record,
                    expectedStepKey ?? record.ExecutionStepKey ?? string.Empty,
                    state,
                    cancellationToken);

                Logger.Engine.StepCompleted(
                    record,
                    pipelineResult.ExecutedStepName);

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
                    record.CurrentStep ?? "unknown",
                    ex);

                await Store.TryUpdateAsync(
                    record.ExecutionId,
                    expectedStepKey ?? record.ExecutionStepKey ?? string.Empty,
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

        /// <inheritdoc />
        public override Task<AiExecutionRecord> ExecuteBatchAsync(
            string executionId,
            int maxSteps,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

            // Sequential execution intentionally processes
            // a single step at a time.
            return ExecuteNextAsync(
                executionId,
                cancellationToken);
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
            }
            while (!record.IsTerminal);

            return record;
        }

        /// <summary>
        /// Rotates the RBAC context key after a successful sequential step execution.
        /// </summary>
        private async Task RotateContextAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken)
        {
            var (newKey, _) = await ContextStore.RotateAsync(
                record.ContextKey,
                ContextRotationOverlap);

            record.ContextKey = newKey;
            record.RenewExecutionStepKey();
        }

        /// <summary>
        /// Advances the sequential orchestration record using the pipeline result.
        /// </summary>
        private static void MoveNext(
            AiExecutionRecord record,
            PipelineExecutionResult pipelineResult)
        {
            record.CurrentStepIndex = pipelineResult.NextStepIndex;

            if (pipelineResult.IsCompleted)
            {
                record.MarkCompleted();
                record.CurrentStep = string.Empty;
            }
            else
            {
                record.MarkRunning();
                record.CurrentStep = pipelineResult.NextStepName ?? string.Empty;
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
    }
}