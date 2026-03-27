using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Coordinates persisted execution of AI pipelines using the RBAC context store.
    ///
    /// Architecture summary:
    /// AiExecutionEngine
    ///     -> IAiExecutionStore
    ///     -> IContextStore
    ///     -> IExecutionContextAccessor
    ///     -> IExecutionContextFactory
    ///     -> IAiPipelineExecutor
    ///
    /// Responsibilities:
    /// - Create a new durable AI execution
    /// - Seed an AI-owned RBAC execution context
    /// - Execute one step at a time safely
    /// - Delegate pipeline step execution to the pipeline executor
    /// - Rotate the RBAC context key between successful steps
    /// - Persist execution record and execution state transitions
    ///
    /// This engine owns orchestration and persistence.
    /// It does not own pipeline resolution details or step business logic.
    /// </summary>
    public sealed class AiExecutionEngine : IAiExecutionEngine
    {
        private static readonly TimeSpan ContextRotationOverlap = TimeSpan.FromSeconds(30);

        private readonly IAiExecutionStore _store;
        private readonly IContextStore _contextStore;
        private readonly IExecutionContextAccessor _accessor;
        private readonly IExecutionContextFactory _contextFactory;
        private readonly IServiceProvider _services;
        private readonly IAiPipelineExecutor _pipelineExecutor;
        private readonly IAiRuntimeLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionEngine"/> class.
        /// </summary>
        /// <param name="store">The durable AI execution store.</param>
        /// <param name="contextStore">The RBAC execution context store.</param>
        /// <param name="accessor">The accessor for the live RBAC execution context.</param>
        /// <param name="contextFactory">The factory used to copy and snapshot RBAC contexts.</param>
        /// <param name="services">The root runtime service provider.</param>
        /// <param name="pipelineExecutor">The pipeline executor responsible for pipeline-specific step execution.</param>
        /// <param name="logger">The centralized AI runtime logger responsible for structured tracing across engine, pipeline, and step execution.</param>
        public AiExecutionEngine(
            IAiExecutionStore store,
            IContextStore contextStore,
            IExecutionContextAccessor accessor,
            IExecutionContextFactory contextFactory,
            IServiceProvider services,
            IAiPipelineExecutor pipelineExecutor,
            IAiRuntimeLogger logger)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(contextStore);
            ArgumentNullException.ThrowIfNull(accessor);
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pipelineExecutor);
            ArgumentNullException.ThrowIfNull(logger);

            _store = store;
            _contextStore = contextStore;
            _accessor = accessor;
            _contextFactory = contextFactory;
            _services = services;
            _pipelineExecutor = pipelineExecutor;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new AI execution together with a separate persisted execution state.
        /// </summary>
        /// <param name="input">The initial input passed to the AI workflow.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created execution record.</returns>
        public Task<AiExecutionRecord> CreateAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "CreateAsync(input) is no longer supported without an explicit pipeline name.");
        }

        /// <summary>
        /// Creates a new AI execution together with a separate persisted execution state
        /// for the specified pipeline.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name associated with the execution.</param>
        /// <param name="input">The initial input passed to the AI workflow.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created execution record.</returns>
        public async Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));
            }

            var current = _accessor.Current
                ?? throw new InvalidOperationException("No active RBAC context is available.");

            var preparedPipeline = await _pipelineExecutor.PrepareAsync(
                pipelineName,
                cancellationToken);

            var orderedSteps = preparedPipeline.Steps
                .OrderBy(x => x.Order)
                .ToArray();

            if (orderedSteps.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' does not contain any resolved steps.");
            }


            var newContextKey = Guid.NewGuid().ToString("N");
            var aiOwnedContext = _contextFactory.CreateCopy(current, newContextKey);
            newContextKey = await _contextStore.SeedAsync(aiOwnedContext);

            var record = new AiExecutionRecord
            {
                PipelineName = pipelineName,
                ContextKey = newContextKey,
                CurrentStep = orderedSteps[0].Name,
                CurrentStepIndex = 0,
                Status = AiExecutionStatus.Pending,
                ExecutionContextSnapshot = _contextFactory.CreateSnapshot(current),
                Steps = orderedSteps.Select(x => x.Name).ToList()
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId,
                PipelineName = pipelineName
            };

            state.Set(AiExecutionKeys.Input, input);

            await _store.CreateAsync(record, state, cancellationToken);

            _logger.Engine.ExecutionCreated(record);

            return record;
        }

        /// <summary>
        /// Executes the next step of the specified AI execution.
        ///
        /// Important:
        /// - The live RBAC execution context is always loaded from the RBAC store using ContextKey.
        /// - Execution state is loaded independently from the AI execution store.
        /// - The RBAC ContextKey is rotated after each successful step.
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated execution record.</returns>
        public async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));
            }

            var record = await _store.GetRecordAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException("Execution not found.");

            var state = await _store.GetStateAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException("Execution state not found.");

            if (record.IsTerminal)
            {
                return record;
            }

            if (string.IsNullOrWhiteSpace(record.PipelineName))
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' does not define a pipeline name.");
            }

            var expectedStepKey = record.ExecutionStepKey;
            var rbacContext = await LoadContextAsync(record.ContextKey);
            var executionContext = BuildExecutionContext(record, state, cancellationToken);

            var resolvedPipeline = await _pipelineExecutor.PrepareAsync(
                record.PipelineName,
                cancellationToken);

            _accessor.Set(rbacContext);

            try
            {
                var pipelineResult = await _pipelineExecutor.ExecuteNextAsync(
                    resolvedPipeline,
                    executionContext,
                    cancellationToken);

                if (!pipelineResult.StepResult.Success)
                {
                    record.MarkFailed();

                    _logger.Engine.StepFailed(
                        record.ExecutionId,
                        record.CurrentStep,
                        pipelineResult.StepResult.Error);

                    record.TouchVersion();
                    record.RenewExecutionStepKey();

                    await _store.TryUpdateAsync(
                        record.ExecutionId,
                        expectedStepKey,
                        record,
                        state,
                        cancellationToken);

                    return record;
                }

                MergeResult(state, pipelineResult.StepResult);

                if (!string.IsNullOrWhiteSpace(pipelineResult.ExecutedStepName))
                {
                    record.CompletedSteps.Add(pipelineResult.ExecutedStepName);
                }

                record.Steps = pipelineResult.Steps.ToList();

                await RotateContextAsync(record, cancellationToken);
                MoveNext(record, pipelineResult);

                // Important:
                // renew the execution transition key before persisting
                // so concurrent callers cannot commit the same transition twice.
                record.TouchVersion();
                record.RenewExecutionStepKey();

                var updated = await _store.TryUpdateAsync(
                    record.ExecutionId,
                    expectedStepKey,
                    record,
                    state,
                    cancellationToken);

                if (!updated)
                {
                    throw new InvalidOperationException("Concurrency conflict on execution update.");
                }

                _logger.Engine.StepCompleted(
                    record,
                    pipelineResult.ExecutedStepName);

                return record;
            }
            catch (Exception ex)
            {
                record.MarkFailed();
                record.TouchVersion();
                record.RenewExecutionStepKey();

                _logger.Engine.StepException(
                    record.ExecutionId,
                    record.CurrentStep,
                    ex);

                await _store.TryUpdateAsync(
                    record.ExecutionId,
                    expectedStepKey,
                    record,
                    state,
                    cancellationToken);

                throw;
            }
            finally
            {
                _accessor.Clear();
            }
        }

        /// <summary>
        /// Executes the remaining pipeline until a terminal state is reached.
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The final execution record.</returns>
        public async Task<AiExecutionRecord> ExecuteAllAsync(
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
        /// Loads the live RBAC execution context associated with the supplied context key.
        /// </summary>
        private async Task<ExecutionContext> LoadContextAsync(string contextKey)
        {
            var context = await _contextStore.GetAsync(contextKey);

            if (context is null)
            {
                throw new InvalidOperationException("RBAC execution context not found.");
            }

            return context;
        }

        /// <summary>
        /// Builds the shared AI execution context used by the current step.
        /// </summary>
        private AiExecutionContext BuildExecutionContext(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            return new AiExecutionContext(
                record,
                state,
                _services,
                cancellationToken);
        }

        /// <summary>
        /// Merges a successful step result into the persisted execution state.
        /// </summary>
        private static void MergeResult(
            AiExecutionState state,
            AiStepResult result)
        {
            if (result.Data.Count == 0)
            {
                return;
            }

            foreach (var entry in result.Data)
            {
                state.Set(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Rotates the RBAC context key after a successful step execution.
        /// </summary>
        private async Task RotateContextAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken)
        {
            var (newKey, _) = await _contextStore.RotateAsync(
                record.ContextKey,
                ContextRotationOverlap);

            record.ContextKey = newKey;
            record.RenewExecutionStepKey();
        }

        /// <summary>
        /// Advances the orchestration record using the pipeline execution result,
        /// or marks the execution as completed.
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
    }
}