using System;
using System.Linq;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Steps;
using Multiplexed.AI.Stores;
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
    /// - update persisted execution record and execution state
    /// </summary>
    public sealed class AiExecutionEngine
    {
        private readonly List<IAiStep> _steps;
        private readonly IAiExecutionStore _store;
        private readonly IContextStore _contextStore;
        private readonly IRuntimeEventContext _realtime;
        private readonly IExecutionContextAccessor _accessor;
        private readonly IExecutionContextFactory _contextFactory;

        public AiExecutionEngine(
            IEnumerable<IAiStep> steps,
            IAiExecutionStore store,
            IContextStore contextStore,
            IRuntimeEventContext realtime,
            IExecutionContextAccessor accessor,
            IExecutionContextFactory contextFactory)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(contextStore);
            ArgumentNullException.ThrowIfNull(realtime);
            ArgumentNullException.ThrowIfNull(accessor);
            ArgumentNullException.ThrowIfNull(contextFactory);


            _steps = steps.ToList();
            _store = store;
            _contextStore = contextStore;
            _realtime = realtime;
            _accessor = accessor;
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Creates a new AI execution and a separate persisted execution state.
        /// </summary>
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

            var aiContext = _contextFactory.CreateCopy(current, String.Empty);

            var newKey = await _contextStore.SeedAsync(aiContext);

            var record = new AiExecutionRecord
            {
                ContextKey = newKey,
                CurrentStep = firstStep.Name,
                Status = "Pending",
                ExecutionContextSnapshot = _contextFactory.CreateSnapshot(current)
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId
            };

            state.Data["input"] = input;

            await _store.CreateAsync(record, state, ct);

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
        /// Executes the next step of the specified AI execution.
        /// 
        /// IMPORTANT:
        /// - The live RBAC execution context is always loaded from the RBAC store using ContextKey.
        /// - Execution state is loaded from the AI execution store.
        /// - ContextKey is rotated after each successful step.
        /// </summary>
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

            if (string.Equals(record.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return record;

            var step = ResolveStep(record.CurrentStep);
            var expectedStepKey = record.ExecutionStepKey;
            var context = await LoadContext(record.ContextKey);
            var pipelineContext = BuildPipelineContext(record.ExecutionId, state);

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

                await _store.TryUpdateAsync(record.ExecutionId, expectedStepKey, record, state, ct);
                return record;
            }

            MergeResult(state, pipelineContext, result);

            record.CompletedSteps.Add(record.CurrentStep);
            record.Version++;

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
        /// Executes the full pipeline from the current step until completion or failure.
        /// </summary>
        public async Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken ct = default)
        {
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
        /// Resolves the current step by name.
        /// </summary>
        private IAiStep ResolveStep(string stepName)
        {
            return _steps.FirstOrDefault(x =>
                string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Step '{stepName}' not found.");
        }

        /// <summary>
        /// Loads the current RBAC execution context from the shared RBAC context store.
        /// </summary>
        private async Task<Rbac.Core.ExecutionContext.ExecutionContext> LoadContext(string key)
        {
            var context = await _contextStore.GetAsync(key);

            if (context is null)
                throw new InvalidOperationException("RBAC execution context not found.");

            return context;
        }

        /// <summary>
        /// Builds a fresh pipeline context from the persisted execution state.
        /// </summary>
        private AiStepContext BuildPipelineContext(
            string executionId,
            AiExecutionState state)
        {
            var context = new AiStepContext
            {
                ExecutionId = executionId
            };

            foreach (var entry in state.Data)
                context.Data[entry.Key] = entry.Value;

            foreach (var entry in state.Metadata)
                context.Metadata[entry.Key] = entry.Value;

            return context;
        }

        /// <summary>
        /// Executes the specified step and emits a start event.
        /// </summary>
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
        /// Merges the step result into the persisted execution state.
        /// </summary>
        private void MergeResult(
            AiExecutionState state,
            AiStepContext context,
            AiStepResult result)
        {
            foreach (var entry in result.Data)
                context.Data[entry.Key] = entry.Value;

            state.Data = context.Data;
            state.Metadata = context.Metadata;
            state.UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Rotates the RBAC context key after successful step execution.
        /// </summary>
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