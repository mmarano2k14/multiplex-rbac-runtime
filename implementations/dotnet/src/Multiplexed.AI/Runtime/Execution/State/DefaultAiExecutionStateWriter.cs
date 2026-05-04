using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.Metrics;

namespace Multiplexed.AI.Runtime.Execution.State
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionStateWriter"/>.
    ///
    /// PURPOSE:
    /// - Applies all durable execution state mutations in one place.
    /// - Keeps <see cref="AiExecutionState"/> focused on persistence shape.
    /// - Ensures mutation timestamps are updated consistently.
    ///
    /// OBSERVABILITY:
    /// - Records hot-state step additions when runtime metrics are available.
    /// - Records hot-state size observations after step mutations.
    ///
    /// IMPORTANT:
    /// - Metrics are optional and observational only.
    /// - Metrics must never influence state mutation behavior.
    /// - This writer does not apply retention or remove steps from hot state.
    /// </summary>
    public sealed class DefaultAiExecutionStateWriter : IAiExecutionStateWriter
    {
        private readonly IAiRuntimeMetrics? _metrics;
        private readonly IAiRetryPolicyDefinitionResolver? _retryDefinitionResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionStateWriter"/> class.
        ///
        /// PURPOSE:
        /// - Preserves existing behavior without requiring metrics or retry registration.
        /// </summary>
        public DefaultAiExecutionStateWriter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionStateWriter"/> class
        /// with runtime metrics support.
        ///
        /// PURPOSE:
        /// - Enables hot-state observability without changing mutation semantics.
        /// </summary>
        public DefaultAiExecutionStateWriter(IAiRuntimeMetrics metrics)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionStateWriter"/> class
        /// with retry policy definition resolution support.
        ///
        /// PURPOSE:
        /// - Enables retry configuration hydration during step initialization.
        /// - Preserves execution behavior by only attaching retry metadata to step state.
        /// </summary>
        public DefaultAiExecutionStateWriter(IAiRetryPolicyDefinitionResolver retryDefinitionResolver)
        {
            _retryDefinitionResolver = retryDefinitionResolver ?? throw new ArgumentNullException(nameof(retryDefinitionResolver));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionStateWriter"/> class
        /// with runtime metrics support and retry policy definition resolution support.
        ///
        /// PURPOSE:
        /// - Enables hot-state observability without changing mutation semantics.
        /// - Enables retry configuration hydration from resolved step configuration.
        /// - Does not change retry execution behavior by itself.
        /// </summary>
        public DefaultAiExecutionStateWriter(
            IAiRuntimeMetrics metrics,
            IAiRetryPolicyDefinitionResolver retryDefinitionResolver)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _retryDefinitionResolver = retryDefinitionResolver ?? throw new ArgumentNullException(nameof(retryDefinitionResolver));
        }

        /// <inheritdoc />
        public void SetData<T>(AiExecutionState state, string key, T value)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            state.Data[key] = value;
            Touch(state);
            RecordStateSizeObserved(state);
        }

        /// <inheritdoc />
        public bool RemoveData(AiExecutionState state, string key)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var removed = state.Data.Remove(key);

            if (removed)
            {
                Touch(state);
                RecordStateSizeObserved(state);
            }

            return removed;
        }

        /// <inheritdoc />
        public void SetDataPayload(AiExecutionState state, string key, AiStoredPayload payload)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(payload);

            state.DataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);
            state.DataPayloads[key] = payload;

            Touch(state);
            RecordStateSizeObserved(state);
        }

        /// <inheritdoc />
        public bool RemoveDataPayload(AiExecutionState state, string key)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var removed = state.DataPayloads != null &&
                          state.DataPayloads.Remove(key);

            if (removed)
            {
                Touch(state);
                RecordStateSizeObserved(state);
            }

            return removed;
        }

        /// <inheritdoc />
        public void SetMetadata<T>(AiExecutionState state, string key, T value)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            state.Metadata[key] = value;
            Touch(state);
            RecordStateSizeObserved(state);
        }

        /// <inheritdoc />
        public bool RemoveMetadata(AiExecutionState state, string key)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var removed = state.Metadata.Remove(key);

            if (removed)
            {
                Touch(state);
                RecordStateSizeObserved(state);
            }

            return removed;
        }

        /// <inheritdoc />
        public void SetMetadataPayload(AiExecutionState state, string key, AiStoredPayload payload)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(payload);

            state.MetadataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);
            state.MetadataPayloads[key] = payload;

            Touch(state);
            RecordStateSizeObserved(state);
        }

        /// <inheritdoc />
        public bool RemoveMetadataPayload(AiExecutionState state, string key)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var removed = state.MetadataPayloads != null &&
                          state.MetadataPayloads.Remove(key);

            if (removed)
            {
                Touch(state);
                RecordStateSizeObserved(state);
            }

            return removed;
        }

        /// <inheritdoc />
        public void EnsureStepInitialized(
            AiExecutionState state,
            ResolvedAiPipelineStep stepDefinition)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepDefinition);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepDefinition.Name);

            if (state.Steps.ContainsKey(stepDefinition.Name))
            {
                return;
            }

            var beforeSteps = state.Steps.Count;

            var stepState = new AiStepState
            {
                StepName = stepDefinition.Name
            };

            state.Steps[stepDefinition.Name] = stepState;

            stepState.SetInputs(stepDefinition.Input);
            stepState.SetConfig(stepDefinition.Config);

            // REHYDRATE ???

            stepState.Retry = _retryDefinitionResolver?.Resolve(stepState.Config);

            if (stepState.Retry is not null)
            {
                stepState.RetryState ??= new AiStepRetryState();
            }

            stepState.UpdatedAtUtc = DateTime.UtcNow;

            Touch(state);

            _metrics?.HotState.RecordStateStepAdded(
                state.ExecutionId,
                stepDefinition.Name);

            RecordStateSizeObserved(state);
        }

        /// <inheritdoc />
        public AiStepState GetOrCreateStep(AiExecutionState state, string stepName)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            if (state.Steps.TryGetValue(stepName, out var existing))
            {
                return existing;
            }

            var stepState = new AiStepState
            {
                StepName = stepName
            };

            state.Steps[stepName] = stepState;
            Touch(state);

            _metrics?.HotState.RecordStateStepAdded(
                state.ExecutionId,
                stepName);

            RecordStateSizeObserved(state);

            return stepState;
        }

        /// <inheritdoc />
        public void SetStepResult(
            AiExecutionState state,
            string stepName,
            AiStepResult result)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(result);

            if (!state.Steps.TryGetValue(stepName, out var stepState))
            {
                throw new InvalidOperationException($"Step '{stepName}' is not initialized.");
            }

            var now = DateTime.UtcNow;

            stepState.Result = result;
            stepState.UpdatedAtUtc = now;
            state.UpdatedAtUtc = now;

            RecordStateSizeObserved(state);
        }

        /// <summary>
        /// Updates the state mutation timestamp.
        /// </summary>
        private static void Touch(AiExecutionState state)
        {
            state.UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Records the current hot-state size when metrics are available.
        ///
        /// IMPORTANT:
        /// - This uses step count only.
        /// - Estimated byte size is intentionally omitted because this writer does not serialize state.
        /// </summary>
        private void RecordStateSizeObserved(AiExecutionState state)
        {
            _metrics?.HotState.RecordStateSizeObserved(
                state.ExecutionId,
                state.Steps.Count,
                estimatedBytes: null);
        }
    }
}