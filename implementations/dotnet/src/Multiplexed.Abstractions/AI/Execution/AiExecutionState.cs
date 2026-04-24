using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the mutable working state of an AI execution.
    ///
    /// PURPOSE:
    /// - Stores the mutable runtime payload exchanged across the execution
    /// - Holds durable step-scoped state for orchestration and execution
    /// - Separates working state from <see cref="AiExecutionRecord"/>, which remains
    ///   the orchestration summary and lifecycle projection
    ///
    /// DESIGN:
    /// - <see cref="Data"/> is the legacy shared execution bag
    /// - <see cref="Steps"/> contains durable per-step state and is the source of truth
    ///   for step-scoped execution and DAG orchestration
    /// - <see cref="Metadata"/> stores technical/runtime hints and diagnostics,
    ///   not business-critical pipeline payload
    ///
    /// PAYLOAD EVOLUTION:
    /// - <see cref="DataPayloads"/> and <see cref="MetadataPayloads"/> introduce
    ///   payload-backed storage without removing the legacy inline bags
    /// - Payload-backed values are additive and intended for progressive ledger compaction
    /// - Existing callers using <see cref="Data"/> and <see cref="Metadata"/> remain supported
    ///
    /// IMPORTANT:
    /// - This object is intended to be safely persisted and restored
    /// - It supports replay, recovery, and distributed execution scenarios
    /// - Newer runtime flows should prefer step-scoped state over the legacy shared bag
    /// </summary>
    public sealed class AiExecutionState
    {
        /// <summary>
        /// Gets or sets the unique identifier of the execution state.
        ///
        /// This typically matches the parent execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the optional pipeline name associated with this execution state.
        ///
        /// This identifies the workflow definition used to resolve the executable runtime pipeline.
        /// </summary>
        public string? PipelineName { get; set; }

        /// <summary>
        /// Gets or sets the legacy shared execution data bag.
        ///
        /// PURPOSE:
        /// - Stores global execution data exchanged across steps
        /// - Preserved for compatibility with older runtime flows
        ///
        /// IMPORTANT:
        /// - Keys should remain stable across steps
        /// - Newer step-aware flows should prefer <see cref="Steps"/> when possible
        /// - Large or unbounded payloads should progressively move to <see cref="DataPayloads"/>
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the optional payload-backed representation of shared execution data.
        ///
        /// PURPOSE:
        /// - Enables large or unbounded execution data to be represented by compact payload references
        /// - Supports future ledger compaction without removing the legacy <see cref="Data"/> bag
        ///
        /// COMPATIBILITY:
        /// - Existing callers may continue using <see cref="Get{T}"/>, <see cref="Set{T}"/>,
        ///   <see cref="TryGet{T}"/>, and <see cref="Data"/>
        /// - Payload-aware callers should use <see cref="GetDataAsync{T}"/> or
        ///   <see cref="GetData{T}"/>
        ///
        /// PRECEDENCE:
        /// - When a key exists in both <see cref="DataPayloads"/> and <see cref="Data"/>,
        ///   the payload-backed value takes priority in payload-aware accessors
        /// </summary>
        public Dictionary<string, AiStoredPayload>? DataPayloads { get; set; }

        /// <summary>
        /// Gets or sets the durable per-step runtime state.
        ///
        /// Each entry is keyed by logical step name and contains the mutable execution
        /// state for that step, including inputs, config, result, retry state, and status.
        /// </summary>
        public Dictionary<string, AiStepState> Steps { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets additional execution metadata used for diagnostics, tracing,
        /// orchestration hints, or transport-related concerns.
        ///
        /// IMPORTANT:
        /// - This bag is intended for technical/runtime metadata
        /// - It should not contain business-critical pipeline payload
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the optional payload-backed representation of execution metadata.
        ///
        /// PURPOSE:
        /// - Enables large diagnostic or runtime metadata values to be externalized
        /// - Keeps <see cref="Metadata"/> fully compatible during migration
        ///
        /// COMPATIBILITY:
        /// - Existing metadata accessors remain unchanged
        /// - Payload-aware metadata access should use <see cref="GetMetadataAsync{T}"/>
        ///   or <see cref="GetMetadata{T}(string, IAiExecutionPayloadResolver)"/>
        ///
        /// PRECEDENCE:
        /// - Payload-backed metadata values take priority in payload-aware accessors
        /// </summary>
        public Dictionary<string, AiStoredPayload>? MetadataPayloads { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp indicating when the execution state was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp indicating the last time the execution state was updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // ------------------------------------------------------------------
        // LEGACY DATA ACCESS API
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed value from the legacy shared execution data bag.
        ///
        /// Behavior:
        /// - Returns <c>default</c> if the key does not exist
        /// - Returns <c>default</c> if the stored value is null
        /// - Throws if the stored value exists but cannot be converted to the requested type
        ///
        /// Also supports values restored from JSON persistence layers as <see cref="JsonElement"/>.
        /// </summary>
        public T? Get<T>(string key)
        {
            return GetValue<T>(Data, key, "ExecutionState");
        }

        /// <summary>
        /// Stores or replaces a value in the legacy shared execution data bag.
        ///
        /// Updates <see cref="UpdatedAtUtc"/>.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            Data[key] = value;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the legacy shared execution data bag.
        ///
        /// Behavior:
        /// - Returns <c>true</c> when the key exists and the value can be converted
        /// - Returns <c>true</c> with <c>default</c> when the key exists but the stored value is null
        /// - Returns <c>false</c> when the key does not exist or conversion fails
        ///
        /// Also supports values restored from JSON persistence layers as <see cref="JsonElement"/>.
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
        /// Updates <see cref="UpdatedAtUtc"/> when removal succeeds.
        /// </summary>
        public void Remove(string key)
        {
            if (Data.Remove(key))
                UpdatedAtUtc = DateTime.UtcNow;
        }

        // ------------------------------------------------------------------
        // PAYLOAD-AWARE DATA ACCESS API
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed value from payload-backed execution data when available,
        /// otherwise falls back to the legacy shared execution data bag.
        ///
        /// BEHAVIOR:
        /// - Payload-backed data takes priority over inline <see cref="Data"/>
        /// - Inline <see cref="Data"/> remains the fallback for compatibility
        /// - The method does not mutate execution state
        /// </summary>
        public async Task<T?> GetDataAsync<T>(
            string key,
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (DataPayloads != null &&
                DataPayloads.TryGetValue(key, out var payload))
            {
                var resolvedValue = await resolver.ResolveAsync(payload, cancellationToken);
                return ConvertValue<T>(resolvedValue);
            }

            return Get<T>(key);
        }

        /// <summary>
        /// Synchronous compatibility helper for retrieving payload-backed execution data
        /// when available, otherwise falling back to the legacy shared execution data bag.
        ///
        /// Prefer <see cref="GetDataAsync{T}"/> in async runtime paths.
        /// </summary>
        public T? GetData<T>(
            string key,
            IAiExecutionPayloadResolver resolver)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (DataPayloads != null &&
                DataPayloads.TryGetValue(key, out var payload))
            {
                var resolvedValue = resolver.ResolveAsync(payload)
                    .GetAwaiter()
                    .GetResult();

                return ConvertValue<T>(resolvedValue);
            }

            return Get<T>(key);
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from payload-backed execution data
        /// when available, otherwise falling back to the legacy shared execution data bag.
        ///
        /// Returns <c>false</c> when the key does not exist or conversion/resolution fails.
        /// </summary>
        public async Task<(bool Success, T? Value)> TryGetDataAsync<T>(
            string key,
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            try
            {
                var value = await GetDataAsync<T>(key, resolver, cancellationToken);
                var exists =
                    (DataPayloads != null && DataPayloads.ContainsKey(key)) ||
                    Data.ContainsKey(key);

                return (exists, value);
            }
            catch
            {
                return (false, default);
            }
        }

        /// <summary>
        /// Stores or replaces a payload-backed execution data entry.
        ///
        /// IMPORTANT:
        /// - This method does not remove the legacy inline <see cref="Data"/> entry
        /// - This preserves compatibility during progressive migration
        /// - Payload-aware accessors will prefer this payload entry when present
        /// </summary>
        public void SetDataPayload(string key, AiStoredPayload payload)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(payload);

            DataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);
            DataPayloads[key] = payload;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Removes a payload-backed execution data entry if it exists.
        ///
        /// IMPORTANT:
        /// - This does not remove the legacy inline <see cref="Data"/> entry
        /// - Use <see cref="Remove"/> to remove inline data
        /// </summary>
        public void RemoveDataPayload(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (DataPayloads != null && DataPayloads.Remove(key))
                UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Applies a resolved step definition to the execution state by initializing
        /// or updating the corresponding <see cref="AiStepState"/>.
        ///
        /// This method:
        /// - Ensures the durable step state exists
        /// - Copies resolved Inputs and Config into runtime state
        /// - Applies resolved retry policy values
        /// - Updates timestamps for traceability
        ///
        /// IMPORTANT:
        /// - This does not execute the step
        /// - This does not set the step result
        /// - Inputs and Config are treated as runtime-ready copies of the resolved step definition
        /// </summary>
        /// <param name="stepDefinition">The resolved pipeline step definition.</param>
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

            // Apply resolved retry policy to durable runtime state.
            // These values come from the resolved step, so defaults have already been applied
            // by pipeline resolution when the definition did not explicitly configure retry.
            stepState.MaxRetries = stepDefinition.MaxRetries;
            stepState.RetryDelay = TimeSpan.FromMilliseconds(stepDefinition.RetryDelayMs);

            stepState.UpdatedAtUtc = DateTime.UtcNow;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Ensures that a step is initialized in the execution state.
        ///
        /// Behavior:
        /// - If the step does not exist, it is created and initialized from the resolved definition
        /// - If the step already exists, it is preserved as-is
        ///
        /// IMPORTANT:
        /// - This method is intentionally idempotent
        /// - Existing runtime data such as result, inputs, config, or retry state is not overwritten
        /// - Use this during pipeline preparation, binding, or DAG initialization
        /// </summary>
        /// <param name="stepDefinition">The resolved pipeline step definition.</param>
        public void EnsureStepInitialized(ResolvedAiPipelineStep stepDefinition)
        {
            ArgumentNullException.ThrowIfNull(stepDefinition);

            if (Steps.ContainsKey(stepDefinition.Name))
                return;

            ApplyStepDefinition(stepDefinition);
        }

        /// <summary>
        /// Gets the existing durable step state or creates a new one when missing.
        ///
        /// This helper is useful when orchestration needs to inspect or mutate step state
        /// without requiring a full resolved pipeline step definition.
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
        ///
        /// Returns <c>default</c> when the step or key does not exist.
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
        /// Attempts to retrieve a strongly-typed resolved input value for the specified step.
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
        ///
        /// Returns <c>default</c> when the step or key does not exist.
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
        /// Attempts to retrieve a strongly-typed configuration value for the specified step.
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
        /// Retrieves a strongly-typed value from the execution metadata bag.
        ///
        /// Returns <c>default</c> when the key does not exist or the stored value is null.
        /// Throws when a present value cannot be converted to the requested type.
        ///
        /// Also supports values restored from JSON persistence layers as <see cref="JsonElement"/>.
        /// </summary>
        public T? GetMetadata<T>(string key)
        {
            return GetValue<T>(Metadata, key, "ExecutionMetadata");
        }

        /// <summary>
        /// Stores or replaces a value in the execution metadata bag.
        ///
        /// Updates <see cref="UpdatedAtUtc"/>.
        /// </summary>
        public void SetMetadata<T>(string key, T value)
        {
            Metadata[key] = value;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the execution metadata bag.
        ///
        /// Also supports values restored from JSON persistence layers as <see cref="JsonElement"/>.
        /// </summary>
        public bool TryGetMetadata<T>(string key, out T? value)
        {
            return TryGetValue(Metadata, key, "ExecutionMetadata", out value);
        }

        /// <summary>
        /// Determines whether a key exists in the execution metadata bag.
        /// </summary>
        public bool ContainsMetadata(string key) => Metadata.ContainsKey(key);

        /// <summary>
        /// Removes a value from the execution metadata bag if it exists.
        ///
        /// Updates <see cref="UpdatedAtUtc"/> when removal succeeds.
        /// </summary>
        public void RemoveMetadata(string key)
        {
            if (Metadata.Remove(key))
                UpdatedAtUtc = DateTime.UtcNow;
        }

        // ------------------------------------------------------------------
        // PAYLOAD-AWARE METADATA ACCESS API
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves metadata using payload resolution when available,
        /// otherwise falls back to the inline metadata bag.
        ///
        /// BEHAVIOR:
        /// - Payload-backed metadata takes priority over inline <see cref="Metadata"/>
        /// - Inline metadata remains the fallback for compatibility
        /// - The method does not mutate execution state
        /// </summary>
        public async Task<T?> GetMetadataAsync<T>(
            string key,
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (MetadataPayloads != null &&
                MetadataPayloads.TryGetValue(key, out var payload))
            {
                var resolvedValue = await resolver.ResolveAsync(payload, cancellationToken);
                return ConvertValue<T>(resolvedValue);
            }

            return GetMetadata<T>(key);
        }

        /// <summary>
        /// Synchronous compatibility helper for retrieving payload-backed metadata
        /// when available, otherwise falling back to the inline metadata bag.
        ///
        /// Prefer <see cref="GetMetadataAsync{T}"/> in async runtime paths.
        /// </summary>
        public T? GetMetadata<T>(
            string key,
            IAiExecutionPayloadResolver resolver)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (MetadataPayloads != null &&
                MetadataPayloads.TryGetValue(key, out var payload))
            {
                var resolvedValue = resolver.ResolveAsync(payload)
                    .GetAwaiter()
                    .GetResult();

                return ConvertValue<T>(resolvedValue);
            }

            return GetMetadata<T>(key);
        }

        /// <summary>
        /// Stores or replaces a payload-backed metadata entry.
        ///
        /// IMPORTANT:
        /// - This method does not remove the legacy inline <see cref="Metadata"/> entry
        /// - This preserves compatibility during progressive migration
        /// </summary>
        public void SetMetadataPayload(string key, AiStoredPayload payload)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(payload);

            MetadataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);
            MetadataPayloads[key] = payload;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Removes a payload-backed metadata entry if it exists.
        ///
        /// IMPORTANT:
        /// - This does not remove the legacy inline <see cref="Metadata"/> entry
        /// - Use <see cref="RemoveMetadata"/> to remove inline metadata
        /// </summary>
        public void RemoveMetadataPayload(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (MetadataPayloads != null && MetadataPayloads.Remove(key))
                UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets or replaces the execution result for the specified step.
        ///
        /// Throws when the step has not been initialized.
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
        /// This is required when execution state is restored from persistence layers
        /// such as Redis, because <see cref="System.Text.Json"/> deserializes
        /// object-based dictionary values as <see cref="JsonElement"/>.
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

        /// <summary>
        /// Converts a materialized payload value into the requested type.
        ///
        /// Supports:
        /// - direct typed runtime values
        /// - null values
        /// - JSON-backed values restored as <see cref="JsonElement"/>
        /// - simple convertible primitives
        ///
        /// IMPORTANT:
        /// - This helper is used by payload-aware accessors only
        /// - Existing legacy accessors continue to use <see cref="GetValue{T}"/>
        ///   and <see cref="TryGetValue{T}"/>
        /// </summary>
        private static T? ConvertValue<T>(object? rawValue)
        {
            if (rawValue is null)
                return default;

            if (rawValue is T typed)
                return typed;

            if (rawValue is JsonElement jsonElement)
                return ConvertJsonElement<T>("payload", jsonElement, "ExecutionPayload");

            return (T?)Convert.ChangeType(rawValue, typeof(T));
        }
    }
}