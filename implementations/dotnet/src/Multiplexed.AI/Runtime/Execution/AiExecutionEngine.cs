using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Coordinates persisted execution of AI steps using the RBAC context store.
    ///
    /// Responsibilities:
    /// - Create a new durable AI execution
    /// - Seed an AI-owned RBAC execution context
    /// - Execute one step at a time safely
    /// - Delegate step execution to the step executor
    /// - Rotate the RBAC context key between successful steps
    /// - Persist execution record and execution state transitions
    ///
    /// This engine is intentionally focused on orchestration and persistence.
    /// It does not own business logic of individual steps.
    /// </summary>
    public sealed class AiExecutionEngine : IAiExecutionEngine
    {
        private const string InputKey = "input";
        private static readonly TimeSpan ContextRotationOverlap = TimeSpan.FromSeconds(30);

        private readonly IReadOnlyList<IAiStep> _steps;
        private readonly IAiExecutionStore _store;
        private readonly IContextStore _contextStore;
        private readonly IRuntimeEventContext _realtime;
        private readonly IExecutionContextAccessor _accessor;
        private readonly IExecutionContextFactory _contextFactory;
        private readonly IServiceProvider _services;
        private readonly IAiStepExecutor _stepExecutor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionEngine"/> class.
        /// </summary>
        /// <param name="steps">The ordered AI steps used by the execution pipeline.</param>
        /// <param name="store">The durable AI execution store.</param>
        /// <param name="contextStore">The RBAC execution context store.</param>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        /// <param name="accessor">The accessor for the live RBAC execution context.</param>
        /// <param name="contextFactory">The factory used to copy and snapshot RBAC contexts.</param>
        /// <param name="services">The root runtime service provider.</param>
        /// <param name="stepExecutor">The executor responsible for resilient single-step execution.</param>
        public AiExecutionEngine(
            IEnumerable<IAiStep> steps,
            IAiExecutionStore store,
            IContextStore contextStore,
            IRuntimeEventContext realtime,
            IExecutionContextAccessor accessor,
            IExecutionContextFactory contextFactory,
            IServiceProvider services,
            IAiStepExecutor stepExecutor)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(contextStore);
            ArgumentNullException.ThrowIfNull(realtime);
            ArgumentNullException.ThrowIfNull(accessor);
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(stepExecutor);

            _steps = steps.ToList();
            _store = store;
            _contextStore = contextStore;
            _realtime = realtime;
            _accessor = accessor;
            _contextFactory = contextFactory;
            _services = services;
            _stepExecutor = stepExecutor;
        }

        /// <summary>
        /// Creates a new AI execution together with a separate persisted execution state.
        /// </summary>
        /// <param name="input">The initial input passed to the AI workflow.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The newly created execution record.</returns>
        public async Task<AiExecutionRecord> CreateAsync(
            string input,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));

            var current = _accessor.Current
                ?? throw new InvalidOperationException("No active RBAC context is available.");

            var firstStep = GetFirstStepOrThrow();

            // Create an AI-owned copy of the current RBAC context.
            // This context becomes the isolated execution scope for the workflow.
            var aiOwnedContext = _contextFactory.CreateCopy(current, string.Empty);
            var newContextKey = await _contextStore.SeedAsync(aiOwnedContext);

            var record = new AiExecutionRecord
            {
                ContextKey = newContextKey,
                CurrentStep = firstStep.Name,
                CurrentStepIndex = 0,
                Status = "Pending",
                ExecutionContextSnapshot = _contextFactory.CreateSnapshot(current),
                Steps = _steps.Select(x => x.Name).ToList()
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId
            };

            state.Set(InputKey, input);

            await _store.CreateAsync(record, state, ct);

            LogExecutionCreated(record);

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
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The updated execution record.</returns>
        public async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var record = await _store.GetRecordAsync(executionId, ct)
                ?? throw new InvalidOperationException("Execution not found.");

            var state = await _store.GetStateAsync(executionId, ct)
                ?? throw new InvalidOperationException("Execution state not found.");

            if (record.IsTerminal)
                return record;

            var step = ResolveStep(record.CurrentStep);
            var expectedStepKey = record.ExecutionStepKey;

            var rbacContext = await LoadContextAsync(record.ContextKey);
            var executionContext = BuildExecutionContext(record, state, ct);

            _accessor.Set(rbacContext);

            try
            {
                var result = await ExecuteStepAsync(step, executionContext, ct);

                if (!result.Success)
                {
                    record.MarkFailed();

                    LogStepFailed(
                        executionId: record.ExecutionId,
                        stepName: step.Name,
                        error: result.Error);

                    await _store.TryUpdateAsync(
                        record.ExecutionId,
                        expectedStepKey,
                        record,
                        state,
                        ct);

                    return record;
                }

                MergeResult(state, result);

                record.CompletedSteps.Add(step.Name);
                record.TouchVersion();

                await RotateContextAsync(record, ct);
                MoveNext(record, step);

                var updated = await _store.TryUpdateAsync(
                    record.ExecutionId,
                    expectedStepKey,
                    record,
                    state,
                    ct);

                if (!updated)
                    throw new InvalidOperationException("Concurrency conflict on execution update.");

                LogStepCompleted(record, step);

                return record;
            }
            catch (Exception ex)
            {
                record.MarkFailed();

                LogStepException(
                    executionId: record.ExecutionId,
                    stepName: step.Name,
                    exception: ex);

                await _store.TryUpdateAsync(
                    record.ExecutionId,
                    expectedStepKey,
                    record,
                    state,
                    ct);

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
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The final execution record.</returns>
        public async Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken ct = default)
        {
            AiExecutionRecord record;

            do
            {
                record = await ExecuteNextAsync(executionId, ct);
            }
            while (!record.IsTerminal);

            return record;
        }

        /// <summary>
        /// Resolves a step by its configured logical name.
        /// </summary>
        private IAiStep ResolveStep(string stepName)
        {
            return _steps.FirstOrDefault(x =>
                string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Step '{stepName}' not found.");
        }

        /// <summary>
        /// Returns the first configured step or throws when no steps are registered.
        /// </summary>
        private IAiStep GetFirstStepOrThrow()
        {
            return _steps.FirstOrDefault()
                ?? throw new InvalidOperationException("No AI steps are registered.");
        }

        /// <summary>
        /// Loads the live RBAC execution context associated with the supplied context key.
        /// </summary>
        private async Task<ExecutionContext> LoadContextAsync(string contextKey)
        {
            var context = await _contextStore.GetAsync(contextKey);

            if (context is null)
                throw new InvalidOperationException("RBAC execution context not found.");

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
        /// Executes a single step through the dedicated step executor.
        /// </summary>
        private async Task<AiStepResult> ExecuteStepAsync(
            IAiStep step,
            AiExecutionContext context,
            CancellationToken ct)
        {
            return await _stepExecutor.ExecuteAsync(step, context, ct);
        }

        /// <summary>
        /// Merges a successful step result into the persisted execution state.
        /// </summary>
        private static void MergeResult(
            AiExecutionState state,
            AiStepResult result)
        {
            if (result.Data.Count == 0)
                return;

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
            CancellationToken ct)
        {
            var (newKey, _) = await _contextStore.RotateAsync(
                record.ContextKey,
                ContextRotationOverlap);

            record.ContextKey = newKey;
            record.RenewExecutionStepKey();
        }

        /// <summary>
        /// Advances the orchestration record to the next step, or marks the execution as completed.
        /// </summary>
        private void MoveNext(
            AiExecutionRecord record,
            IAiStep step)
        {
            var currentIndex = _steps
                .Select((value, index) => new { value, index })
                .First(x => string.Equals(x.value.Name, step.Name, StringComparison.OrdinalIgnoreCase))
                .index;

            var nextIndex = currentIndex + 1;

            if (nextIndex >= _steps.Count)
            {
                record.MarkCompleted();
                record.CurrentStep = string.Empty;
            }
            else
            {
                record.MarkRunning();
                record.CurrentStep = _steps[nextIndex].Name;
            }

            record.CurrentStepIndex = nextIndex;
        }

        /// <summary>
        /// Emits a structured runtime event when a new execution is created.
        /// </summary>
        private void LogExecutionCreated(AiExecutionRecord record)
        {
            _realtime.LogInfo(
                message: "AI execution created.",
                category: "ai.execution.created",
                data: new
                {
                    record.ExecutionId,
                    record.ContextKey,
                    record.CurrentStep
                });
        }

        /// <summary>
        /// Emits a structured runtime event when a step throws an exception.
        /// </summary>
        private void LogStepException(string executionId, string stepName, Exception exception)
        {
            _realtime.LogError(
                message: $"Step '{stepName}' threw an exception.",
                category: "ai.step.exception",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Exception = exception.Message
                });
        }

        /// <summary>
        /// Emits a structured runtime event when a step returns a failed result.
        /// </summary>
        private void LogStepFailed(string executionId, string stepName, string? error)
        {
            _realtime.LogError(
                message: $"Step '{stepName}' failed.",
                category: "ai.step.failed",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Error = error
                });
        }

        /// <summary>
        /// Emits a structured runtime event when a step completes successfully.
        /// </summary>
        private void LogStepCompleted(AiExecutionRecord record, IAiStep step)
        {
            _realtime.LogInfo(
                message: $"Step '{step.Name}' completed.",
                category: "ai.step.completed",
                data: new
                {
                    record.ExecutionId,
                    record.CurrentStep,
                    record.Status
                });
        }
    }
}