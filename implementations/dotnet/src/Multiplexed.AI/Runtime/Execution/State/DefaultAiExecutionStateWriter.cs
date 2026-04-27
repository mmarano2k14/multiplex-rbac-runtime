using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Execution.State
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionStateWriter"/>.
    ///
    /// PURPOSE:
    /// - Applies all durable execution state mutations in one place.
    /// - Keeps <see cref="AiExecutionState"/> focused on persistence shape.
    /// - Ensures mutation timestamps are updated consistently.
    /// </summary>
    public sealed class DefaultAiExecutionStateWriter : IAiExecutionStateWriter
    {
        /// <inheritdoc />
        public void SetData<T>(AiExecutionState state, string key, T value)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            state.Data[key] = value;
            Touch(state);
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

            var stepState = new AiStepState
            {
                StepName = stepDefinition.Name
            };

            state.Steps[stepDefinition.Name] = stepState;

            stepState.SetInputs(stepDefinition.Input);
            stepState.SetConfig(stepDefinition.Config);
            stepState.MaxRetries = stepDefinition.MaxRetries;
            stepState.RetryDelay = TimeSpan.FromMilliseconds(stepDefinition.RetryDelayMs);
            stepState.UpdatedAtUtc = DateTime.UtcNow;

            Touch(state);
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
        }

        /// <summary>
        /// Updates the state mutation timestamp.
        /// </summary>
        private static void Touch(AiExecutionState state)
        {
            state.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}