using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution.Engine
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
            IOptions<AiExecutionCleanupOptions> cleanupOptions)
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

            state.Set(AiExecutionKeys.Input, input);

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
                        record.CurrentStep,
                        pipelineResult.StepResult.Error);

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    await PersistAsync(
                        record,
                        expectedStepKey,
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
                    expectedStepKey,
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