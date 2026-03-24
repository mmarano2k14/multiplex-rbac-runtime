using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Steps;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Coordinates persisted execution of AI steps using the RBAC context store.
    /// 
    /// Responsibilities:
    /// - create a new AI execution
    /// - seed a durable AI-owned RBAC context
    /// - execute one step at a time safely
    /// - rotate the RBAC context key between steps
    /// - update persisted execution state
    /// </summary>
    public sealed class AiExecutionEngine
    {
        private readonly List<IAiStep> _steps;
        private readonly IAiExecutionStore _store;
        private readonly IContextStore _contextStore;
        private readonly IRuntimeEventContext _realtime;
        private readonly IExecutionContextAccessor _accessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionEngine"/> class.
        /// </summary>
        /// <param name="steps">Registered AI steps available for execution.</param>
        /// <param name="store">Persistent AI execution store.</param>
        /// <param name="contextStore">RBAC context store used to resolve and rotate execution contexts.</param>
        /// <param name="realtime">Runtime event context used for tracing and diagnostics.</param>
        /// <param name="accessor">Execution context accessor used to bind the RBAC context during step execution.</param>
        public AiExecutionEngine(
            IEnumerable<IAiStep> steps,
            IAiExecutionStore store,
            IContextStore contextStore,
            IRuntimeEventContext realtime,
            IExecutionContextAccessor accessor)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(contextStore);
            ArgumentNullException.ThrowIfNull(realtime);
            ArgumentNullException.ThrowIfNull(accessor);

            _steps = steps.ToList();
            _store = store;
            _contextStore = contextStore;
            _realtime = realtime;
            _accessor = accessor;
        }

        /// <summary>
        /// Creates a new AI execution record and seeds a new durable AI-owned RBAC context.
        /// 
        /// IMPORTANT:
        /// - The original HTTP context key must not be reused directly.
        /// - A fresh AI context is created and seeded into the RBAC context store.
        /// - The returned ContextKey becomes the only valid runtime key for future AI steps.
        /// </summary>
        /// <param name="input">Initial input data for the AI execution.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Created AI execution record.</returns>
        public async Task<AiExecutionRecord> CreateAsync(
            string input,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));

            var current = _accessor.Current
                ?? throw new InvalidOperationException("No active RBAC context is available.");

            var firstStep = _steps.FirstOrDefault()
                ?? throw new InvalidOperationException("No AI steps are registered.");

            var aiContext = new Rbac.Core.ExecutionContext.ExecutionContext
            {
                ContextKey = string.Empty,
                TenantId = current.TenantId,
                TenantGroupId = current.TenantGroupId,
                UserId = current.UserId,
                Project = current.Project,
                CurrentNamespace = current.CurrentNamespace,
                Namespaces = current.Namespaces?.ToList() ?? []
            };

            var newKey = await _contextStore.SeedAsync(aiContext);

            var record = new AiExecutionRecord
            {
                ContextKey = newKey,
                CurrentStep = firstStep.Name,
                Status = "Pending",
                ExecutionContextSnapshot = new ExecutionContextSnapshot
                {
                    ContextKey = current.ContextKey,
                    TenantId = current.TenantId,
                    TenantGroupId = current.TenantGroupId,
                    UserId = current.UserId,
                    Project = current.Project,
                    CurrentNamespace = current.CurrentNamespace,
                    Namespaces = current.Namespaces?.ToList() ?? [],
                    CreatedAtUtc = DateTime.UtcNow
                }
            };

            record.Data["input"] = input;

            await _store.CreateAsync(record, ct);

            _realtime.LogInfo(
                message: "AI execution created.",
                category: "ai.execution.created",
                data: new
                {
                    record.ExecutionId,
                    record.ContextKey,
                    record.CurrentStep
                });

            return record;
        }

        /// <summary>
        /// Executes the full AI pipeline from the current step until completion.
        /// 
        /// IMPORTANT:
        /// - This method repeatedly calls ExecuteNextAsync.
        /// - It is useful for local or synchronous execution flows.
        /// - For distributed/background execution, ExecuteNextAsync should remain the primary entry point.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Final execution record.</returns>
        public async Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            AiExecutionRecord record;

            do
            {
                record = await ExecuteNextAsync(executionId, ct);
            }
            while (!string.Equals(record.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(record.Status, "Failed", StringComparison.OrdinalIgnoreCase));

            return record;
        }

        /// <summary>
        /// Executes the next step of the specified AI execution.
        /// 
        /// IMPORTANT:
        /// - The RBAC execution context is always loaded from the store using the current ContextKey.
        /// - The snapshot is not used for execution.
        /// - The context key is rotated after each successful step.
        /// - The execution store is updated using optimistic concurrency via ExecutionStepKey.
        /// </summary>
        /// <param name="executionId">Execution identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Updated execution record.</returns>
        public async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var record = await _store.GetAsync(executionId, ct)
                ?? throw new InvalidOperationException("Execution not found.");

            if (string.Equals(record.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return record;

            var step = ResolveStep(record.CurrentStep);
            var expectedStepKey = record.ExecutionStepKey;
            var context = await LoadContext(record.ContextKey);
            var pipelineContext = BuildPipelineContext(record);

            _accessor.Set(context);

            AiStepResult result;

            try
            {
                result = await ExecuteStep(step, record, pipelineContext, ct);
            }
            catch (Exception ex)
            {
                record.Status = "Failed";
                record.UpdatedAtUtc = DateTime.UtcNow;

                _realtime.LogError(
                    message: $"Step '{step.Name}' threw an exception.",
                    category: "ai.step.exception",
                    data: new
                    {
                        record.ExecutionId,
                        Step = step.Name,
                        Exception = ex.Message
                    });

                throw;
            }
            finally
            {
                _accessor.Clear();
            }

            if (!result.Success)
            {
                record.Status = "Failed";
                record.UpdatedAtUtc = DateTime.UtcNow;

                _realtime.LogError(
                    message: $"Step '{step.Name}' failed.",
                    category: "ai.step.failed",
                    data: new
                    {
                        record.ExecutionId,
                        Step = step.Name,
                        result.Error
                    });

                return record;
            }

            MergeResult(record, pipelineContext, result);
            await RotateContextAsync(record, ct);
            MoveNext(record, step);

            var updated = await _store.TryUpdateAsync(
                record.ExecutionId,
                expectedStepKey,
                record,
                ct);

            if (!updated)
                throw new InvalidOperationException("Concurrency conflict on execution update.");

            _realtime.LogInfo(
                message: $"Step '{step.Name}' completed.",
                category: "ai.step.completed",
                data: new
                {
                    record.ExecutionId,
                    record.CurrentStep,
                    record.Status
                });

            return record;
        }

        /// <summary>
        /// Resolves the current step by name.
        /// </summary>
        /// <param name="stepName">Step name to resolve.</param>
        /// <returns>The matching step instance.</returns>
        private IAiStep ResolveStep(string stepName)
        {
            return _steps.FirstOrDefault(x =>
                string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Step '{stepName}' not found.");
        }

        /// <summary>
        /// Loads the current RBAC execution context from the shared context store.
        /// </summary>
        /// <param name="key">RBAC context key.</param>
        /// <returns>Live execution context.</returns>
        private async Task<Rbac.Core.ExecutionContext.ExecutionContext> LoadContext(string key)
        {
            var context = await _contextStore.GetAsync(key);

            if (context is null)
                throw new InvalidOperationException("RBAC execution context not found.");

            return context;
        }

        /// <summary>
        /// Builds a fresh pipeline context from the persisted execution record.
        /// </summary>
        /// <param name="record">Persisted execution record.</param>
        /// <returns>Pipeline context ready for step execution.</returns>
        private AiStepContext BuildPipelineContext(AiExecutionRecord record)
        {
            var context = new AiStepContext
            {
                ExecutionId = record.ExecutionId
            };

            foreach (var entry in record.Data)
                context.Data[entry.Key] = entry.Value;

            foreach (var entry in record.Metadata)
                context.Metadata[entry.Key] = entry.Value;

            return context;
        }

        /// <summary>
        /// Executes the specified step and logs the start of execution.
        /// </summary>
        /// <param name="step">Step to execute.</param>
        /// <param name="record">Current execution record.</param>
        /// <param name="context">Pipeline context.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Step result.</returns>
        private async Task<AiStepResult> ExecuteStep(
            IAiStep step,
            AiExecutionRecord record,
            AiStepContext context,
            CancellationToken ct)
        {
            _realtime.LogInfo(
                message: $"Step '{step.Name}' started.",
                category: "ai.step.start",
                data: new
                {
                    record.ExecutionId,
                    Step = step.Name
                });

            return await step.ExecuteAsync(context, ct);
        }

        /// <summary>
        /// Merges step output into the persisted execution state.
        /// </summary>
        /// <param name="record">Execution record to update.</param>
        /// <param name="context">Pipeline context containing merged data.</param>
        /// <param name="result">Step result.</param>
        private void MergeResult(
            AiExecutionRecord record,
            AiStepContext context,
            AiStepResult result)
        {
            foreach (var entry in result.Data)
                context.Data[entry.Key] = entry.Value;

            record.Data = context.Data;
            record.Metadata = context.Metadata;
            record.CompletedSteps.Add(record.CurrentStep);
            record.Version++;
        }

        /// <summary>
        /// Rotates the RBAC context key after successful step execution.
        /// </summary>
        /// <param name="record">Execution record to update.</param>
        /// <param name="ct">Cancellation token.</param>
        private async Task RotateContextAsync(
            AiExecutionRecord record,
            CancellationToken ct)
        {
            var (newKey, _) = await _contextStore.RotateAsync(
                record.ContextKey,
                TimeSpan.FromSeconds(30));

            record.ContextKey = newKey;
            record.ExecutionStepKey = Guid.NewGuid().ToString("N");
            record.UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Advances the execution to the next step or marks it as completed.
        /// </summary>
        /// <param name="record">Execution record to update.</param>
        /// <param name="step">Step that has just completed.</param>
        private void MoveNext(
            AiExecutionRecord record,
            IAiStep step)
        {
            var currentIndex = _steps.FindIndex(x =>
                string.Equals(x.Name, step.Name, StringComparison.OrdinalIgnoreCase));

            var nextIndex = currentIndex + 1;

            if (nextIndex >= _steps.Count)
            {
                record.Status = "Completed";
                record.CurrentStep = string.Empty;
            }
            else
            {
                record.Status = "Running";
                record.CurrentStep = _steps[nextIndex].Name;
            }

            record.CurrentStepIndex = nextIndex;
        }
    }
}