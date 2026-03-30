using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the mutable working state of an AI execution.
    ///
    /// This object is the central data exchange layer between pipeline steps.
    /// It is intentionally separated from <see cref="AiExecutionRecord"/> to isolate:
    /// 
    /// - Orchestration metadata (lifecycle, steps, status)
    /// - Execution payload (data flowing through the pipeline)
    ///
    /// This separation enables:
    /// - Safe persistence (e.g. Redis, database)
    /// - Replay and recovery scenarios
    /// - Distributed execution models
    ///
    /// Migration note:
    /// - Legacy shared bag access is still available for compatibility
    /// - New step-scoped state is exposed through <see cref="Steps"/>
    /// </summary>
    public sealed class AiExecutionState
    {
        /// <summary>
        /// Unique identifier of the execution state.
        /// Typically matches the parent execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the optional pipeline name associated with this execution state.
        /// This value identifies the workflow definition used to resolve
        /// the executable runtime pipeline.
        /// </summary>
        public string? PipelineName { get; set; }

        /// <summary>
        /// Shared execution data exchanged between steps.
        ///
        /// This is the legacy primary data bag used by the pipeline.
        /// Keys should remain stable across steps.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the structured state of pipeline steps.
        /// Each step keeps its own resolved inputs and produced outputs.
        /// </summary>
        public Dictionary<string, AiStepState> Steps { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Additional execution metadata used for diagnostics, tracing,
        /// orchestration hints, or transport-related concerns.
        ///
        /// This should not contain business-critical data required by steps.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// UTC timestamp indicating when the execution state was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp indicating the last time the state was updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // ------------------------------------------------------------------
        // LEGACY DATA ACCESS API
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed value from the legacy shared execution data bag.
        ///
        /// Returns <c>default</c> if the key does not exist or the value is null.
        /// Throws if the stored value cannot be cast to the requested type.
        ///
        /// Also supports values restored from JSON persistence layers
        /// as <see cref="JsonElement"/>.
        /// </summary>
        public T? Get<T>(string key)
        {
            return GetValue<T>(Data, key, "ExecutionState");
        }

        /// <summary>
        /// Stores or replaces a value in the legacy shared execution data bag.
        ///
        /// Updates the <see cref="UpdatedAtUtc"/> timestamp.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            Data[key] = value;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the legacy shared execution data bag.
        ///
        /// Returns true if the key exists and the value matches the expected type.
        /// Also supports values restored from JSON persistence layers
        /// as <see cref="JsonElement"/>.
        /// </summary>
        public bool TryGet<T>(string key, out T? value)
        {
            return TryGetValue(Data, key, "ExecutionState", out value);
        }

        /// <summary>
        /// Determines whether a key exists in the legacy shared execution data bag.
        /// </summary>
        public bool Contains(string key) => Data.ContainsKey(key);

        /// <summary>
        /// Removes a value from the legacy shared execution data bag if it exists.
        ///
        /// Updates the <see cref="UpdatedAtUtc"/> timestamp if removal succeeds.
        /// </summary>
        public void Remove(string key)
        {
            if (Data.Remove(key))
                UpdatedAtUtc = DateTime.UtcNow;
        }


        /// <summary>
        /// Applies the step definition to the execution state by initializing
        /// or updating the corresponding <see cref="AiStepState"/>.
        ///
        /// This method:
        /// - Ensures the step state exists
        /// - Sets or replaces Inputs and Config from the definition
        /// - Updates timestamps for traceability
        ///
        /// Notes:
        /// - This does NOT execute the step
        /// - This does NOT set the Result
        /// - Inputs/Config are considered runtime-ready copies of the definition
        /// </summary>
        /// <param name="stepDefinition">The pipeline step definition.</param>
        private void ApplyStepDefinition(ResolvedAiPipelineStep stepDefinition)
        {
            ArgumentNullException.ThrowIfNull(stepDefinition);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepDefinition.Name);

            if (!Steps.TryGetValue(stepDefinition.Name, out var stepState))
            {
                stepState = new AiStepState
                {
                    StepName = stepDefinition.Name
                };

                Steps[stepDefinition.Name] = stepState;
            }

            stepState.SetInputs(stepDefinition.Input);
            stepState.SetConfig(stepDefinition.Config);

            stepState.UpdatedAtUtc = DateTime.UtcNow;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Ensures that a step is initialized in the execution state.
        ///
        /// This method:
        /// - Checks whether a step state already exists for the given definition
        /// - Creates and initializes it using <see cref="ApplyStepDefinition"/> if missing
        ///
        /// Notes:
        /// - This method is idempotent: calling it multiple times will not override existing state
        /// - Existing step data (including Result, Inputs, Config) is preserved
        /// - Use this during pipeline initialization or DAG preparation phases
        /// </summary>
        /// <param name="stepDefinition">The pipeline step definition.</param>
        public void EnsureStepInitialized(ResolvedAiPipelineStep stepDefinition)
        {
            ArgumentNullException.ThrowIfNull(stepDefinition);

            if (Steps.ContainsKey(stepDefinition.Name))
                return;

            ApplyStepDefinition(stepDefinition);
        }

        /// <summary>
        /// Gets the existing step state or creates a new one when missing.
        /// </summary>
        public AiStepState GetOrCreateStep(string stepName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            if (!Steps.TryGetValue(stepName, out var stepState))
            {
                stepState = new AiStepState
                {
                    StepName = stepName
                };

                Steps[stepName] = stepState;
                UpdatedAtUtc = DateTime.UtcNow;
            }

            return stepState;
        }

        /// <summary>
        /// Retrieves a strongly-typed resolved input value for the specified step.
        /// Returns default if the step or key does not exist.
        /// </summary>
        public T? GetStepInput<T>(string stepName, string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Steps.TryGetValue(stepName, out var step))
                return default;

            return GetValue<T>(step.Inputs!, key, $"StepInputs[{stepName}]");
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed resolved input value
        /// for the specified step.
        /// </summary>
        public bool TryGetStepInput<T>(string stepName, string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Steps.TryGetValue(stepName, out var step))
            {
                value = default;
                return false;
            }

            return TryGetValue(step.Inputs!, key, $"StepInputs[{stepName}]", out value);
        }

        // ------------------------------------------------------------------
        // STEP CONFIG ACCESS API
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed configuration value for the specified step.
        /// Returns default if the step or key does not exist.
        /// </summary>
        public T? GetStepConfig<T>(string stepName, string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Steps.TryGetValue(stepName, out var step))
                return default;

            return GetValue<T>(
                step.Config as IDictionary<string, object?> ?? new Dictionary<string, object?>(),
                key,
                $"StepConfig[{stepName}]");
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed configuration value
        /// for the specified step.
        /// </summary>
        public bool TryGetStepConfig<T>(string stepName, string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Steps.TryGetValue(stepName, out var step))
            {
                value = default;
                return false;
            }

            var config = step.Config as IDictionary<string, object?>;

            if (config is null)
            {
                value = default;
                return false;
            }

            return TryGetValue(config, key, $"StepConfig[{stepName}]", out value);
        }

        // ------------------------------------------------------------------
        // METADATA ACCESS API
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed value from the execution metadata.
        ///
        /// Returns <c>default</c> if the key does not exist or the value is null.
        /// Throws if the stored value cannot be cast to the requested type.
        ///
        /// Also supports values restored from JSON persistence layers
        /// as <see cref="JsonElement"/>.
        /// </summary>
        public T? GetMetadata<T>(string key)
        {
            return GetValue<T>(Metadata, key, "ExecutionMetadata");
        }

        /// <summary>
        /// Stores or replaces a value in the execution metadata.
        ///
        /// Updates the <see cref="UpdatedAtUtc"/> timestamp.
        /// </summary>
        public void SetMetadata<T>(string key, T value)
        {
            Metadata[key] = value;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the execution metadata.
        ///
        /// Returns true if the key exists and the value matches the expected type.
        /// Also supports values restored from JSON persistence layers
        /// as <see cref="JsonElement"/>.
        /// </summary>
        public bool TryGetMetadata<T>(string key, out T? value)
        {
            return TryGetValue(Metadata, key, "ExecutionMetadata", out value);
        }

        /// <summary>
        /// Determines whether a key exists in the execution metadata.
        /// </summary>
        public bool ContainsMetadata(string key) => Metadata.ContainsKey(key);

        /// <summary>
        /// Removes a value from the execution metadata if it exists.
        ///
        /// Updates the <see cref="UpdatedAtUtc"/> timestamp if removal succeeds.
        /// </summary>
        public void RemoveMetadata(string key)
        {
            if (Metadata.Remove(key))
                UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets or replaces the execution result for the specified step.
        /// Throws if the step is not initialized in the dictionary.
        /// </summary>
        /// <param name="stepName">The unique step name.</param>
        /// <param name="result">The execution result produced by the step.</param>
        public void SetStepResult(string stepName, AiStepResult result)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(result);

            if (!Steps.TryGetValue(stepName, out var stepState))
                throw new InvalidOperationException($"Step '{stepName}' is not initialized.");

            var now = DateTime.UtcNow;

            stepState.Result = result;
            stepState.UpdatedAtUtc = now;
            UpdatedAtUtc = now;
        }

        // ------------------------------------------------------------------
        // INTERNAL HELPERS
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed value from the specified bag.
        ///
        /// Supports both direct runtime values and values restored from
        /// JSON persistence layers as <see cref="JsonElement"/>.
        /// </summary>
        private static T? GetValue<T>(
            IDictionary<string, object?> bag,
            string key,
            string scope)
        {
            if (bag.TryGetValue(key, out var value))
            {
                if (value is T typed)
                    return typed;

                if (value is null)
                    return default;

                if (value is JsonElement jsonElement)
                    return ConvertJsonElement<T>(key, jsonElement, scope);

                throw new InvalidCastException(
                    $"{scope} key '{key}' contains type '{value.GetType().Name}' but was requested as '{typeof(T).Name}'.");
            }

            return default;
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the specified bag.
        ///
        /// Supports both direct runtime values and values restored from
        /// JSON persistence layers as <see cref="JsonElement"/>.
        /// </summary>
        private static bool TryGetValue<T>(
            IDictionary<string, object?> bag,
            string key,
            string scope,
            out T? value)
        {
            if (bag.TryGetValue(key, out var obj))
            {
                if (obj is T typed)
                {
                    value = typed;
                    return true;
                }

                if (obj is null)
                {
                    value = default;
                    return true;
                }

                if (obj is JsonElement jsonElement)
                {
                    try
                    {
                        value = ConvertJsonElement<T>(key, jsonElement, scope);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Converts a JSON-backed value into the requested target type.
        ///
        /// This is required when execution state is restored from persistence
        /// layers such as Redis, because <see cref="System.Text.Json"/> deserializes
        /// <c>object</c>-based dictionary values as <see cref="JsonElement"/>.
        /// </summary>
        private static T? ConvertJsonElement<T>(
            string key,
            JsonElement jsonElement,
            string scope)
        {
            try
            {
                if (typeof(T) == typeof(string) && jsonElement.ValueKind == JsonValueKind.String)
                {
                    object? value = jsonElement.GetString();
                    return (T?)value;
                }

                return jsonElement.Deserialize<T>();
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"{scope} key '{key}' contains JSON value '{jsonElement.ValueKind}' but could not be converted to '{typeof(T).Name}'.",
                    ex);
            }
        }
    }
}