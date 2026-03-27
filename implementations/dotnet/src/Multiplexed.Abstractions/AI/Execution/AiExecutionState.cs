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
        /// This is the primary data bag used by the pipeline.
        /// Keys should remain stable across steps.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

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
        // DATA ACCESS API
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed value from the execution data.
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
        /// Stores or replaces a value in the execution data.
        ///
        /// Updates the <see cref="UpdatedAtUtc"/> timestamp.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            Data[key] = value;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the execution data.
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
        /// Determines whether a key exists in the execution data.
        /// </summary>
        public bool Contains(string key) => Data.ContainsKey(key);

        /// <summary>
        /// Removes a value from the execution data if it exists.
        ///
        /// Updates the <see cref="UpdatedAtUtc"/> timestamp if removal succeeds.
        /// </summary>
        public void Remove(string key)
        {
            if (Data.Remove(key))
                UpdatedAtUtc = DateTime.UtcNow;
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
                // Fast-path for string values to avoid unnecessary generic deserialization.
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