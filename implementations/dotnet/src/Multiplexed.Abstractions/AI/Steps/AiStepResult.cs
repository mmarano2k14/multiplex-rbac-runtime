using Multiplexed.Abstractions.AI.Execution.Payloads;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Steps
{
    /// <summary>
    /// Represents the result of a step execution within the AI pipeline.
    ///
    /// A step result communicates:
    /// - Success or failure
    /// - Optional primary value
    /// - Optional stored payload reference
    /// - Optional human-readable output
    /// - Optional error message
    /// - Optional structured extension data
    ///
    /// This object is designed to remain persistence-friendly for storage
    /// in external stores such as Redis.
    /// </summary>
    public sealed class AiStepResult
    {
        private static readonly Dictionary<string, object?> EmptyData =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Indicates whether the step execution succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the optional primary value produced by the step.
        ///
        /// This value is kept for backward compatibility and should remain
        /// serialization-friendly when persisted.
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Gets or sets the optional stored payload representation.
        ///
        /// PURPOSE:
        /// - Allows future ledger compaction by storing large payloads outside
        ///   the execution state while keeping a compact reference here.
        ///
        /// COMPATIBILITY:
        /// - Existing callers can continue using <see cref="Value"/>.
        /// - New payload-aware callers should prefer resolving this property
        ///   through <see cref="IAiExecutionPayloadResolver"/>.
        /// </summary>
        public AiStoredPayload? Payload { get; set; }

        /// <summary>
        /// Optional textual output produced by the step.
        /// </summary>
        public string? Output { get; set; }

        /// <summary>
        /// Optional error message when the step fails.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Structured data returned by the step.
        /// This acts as an extension payload for additional outputs.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static AiStepResult Ok(
            object? value = null,
            string? output = null,
            Dictionary<string, object?>? data = null)
        {
            return new AiStepResult
            {
                Success = true,
                Value = value,
                Payload = null,
                Output = output,
                Error = null,
                Data = data ?? new Dictionary<string, object?>(EmptyData, StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Creates a successful result using a stored payload.
        /// </summary>
        public static AiStepResult OkPayload(
            AiStoredPayload payload,
            string? output = null,
            Dictionary<string, object?>? data = null)
        {
            ArgumentNullException.ThrowIfNull(payload);

            return new AiStepResult
            {
                Success = true,
                Value = null,
                Payload = payload,
                Output = output,
                Error = null,
                Data = data ?? new Dictionary<string, object?>(EmptyData, StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static AiStepResult Fail(
            string error,
            object? value = null,
            Dictionary<string, object?>? data = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            return new AiStepResult
            {
                Success = false,
                Value = value,
                Payload = null,
                Output = null,
                Error = error,
                Data = data ?? new Dictionary<string, object?>(EmptyData, StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Creates a failed result using a stored payload.
        /// </summary>
        public static AiStepResult FailPayload(
            string error,
            AiStoredPayload payload,
            Dictionary<string, object?>? data = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            ArgumentNullException.ThrowIfNull(payload);

            return new AiStepResult
            {
                Success = false,
                Value = null,
                Payload = payload,
                Output = null,
                Error = error,
                Data = data ?? new Dictionary<string, object?>(EmptyData, StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Creates a successful result with a single extension data entry.
        /// </summary>
        public static AiStepResult Ok(
            string key,
            object? dataValue,
            object? value = null,
            string? output = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return new AiStepResult
            {
                Success = true,
                Value = value,
                Payload = null,
                Output = output,
                Error = null,
                Data = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [key] = dataValue
                }
            };
        }

        /// <summary>
        /// Retrieves the primary value as the specified type.
        /// Returns default if the value is missing or cannot be converted.
        /// Supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public T? GetValue<T>()
        {
            return ConvertValue<T>(Value);
        }

        /// <summary>
        /// Retrieves the primary value as the specified type using payload
        /// resolution when a payload is present.
        ///
        /// IMPORTANT:
        /// - Payload has priority over <see cref="Value"/>.
        /// - Falls back to <see cref="Value"/> for backward compatibility.
        /// </summary>
        public async Task<T?> GetValueAsync<T>(
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resolver);

            if (Payload != null)
            {
                var resolvedValue = await resolver.ResolveAsync(Payload, cancellationToken);
                return ConvertValue<T>(resolvedValue);
            }

            return ConvertValue<T>(Value);
        }

        /// <summary>
        /// Retrieves the primary value as the specified type using payload
        /// resolution when a payload is present.
        ///
        /// This synchronous helper is intended for compatibility scenarios.
        /// Prefer <see cref="GetValueAsync{T}(IAiExecutionPayloadResolver, CancellationToken)"/>
        /// in async runtime paths.
        /// </summary>
        public T? GetValue<T>(IAiExecutionPayloadResolver resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);

            if (Payload != null)
            {
                var resolvedValue = resolver.ResolveAsync(Payload)
                    .GetAwaiter()
                    .GetResult();

                return ConvertValue<T>(resolvedValue);
            }

            return ConvertValue<T>(Value);
        }

        /// <summary>
        /// Attempts to retrieve the primary value as the specified type.
        /// Supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public bool TryGetValue<T>(out T? value)
        {
            try
            {
                value = ConvertValue<T>(Value);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Attempts to retrieve the primary value as the specified type using
        /// payload resolution when a payload is present.
        ///
        /// IMPORTANT:
        /// - Payload has priority over <see cref="Value"/>.
        /// - Falls back to <see cref="Value"/> for backward compatibility.
        /// </summary>
        public async Task<(bool Success, T? Value)> TryGetValueAsync<T>(
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resolver);

            try
            {
                var value = await GetValueAsync<T>(resolver, cancellationToken);
                return (true, value);
            }
            catch
            {
                return (false, default);
            }
        }

        /// <summary>
        /// Retrieves a structured data entry as the specified type.
        /// Returns default if the key does not exist or the value cannot be converted.
        /// Supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public T? GetData<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Data.TryGetValue(key, out var rawValue))
                return default;

            return ConvertValue<T>(rawValue);
        }

        /// <summary>
        /// Attempts to retrieve a structured data entry as the specified type.
        /// Supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public bool TryGetData<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Data.TryGetValue(key, out var rawValue))
            {
                value = default;
                return false;
            }

            try
            {
                value = ConvertValue<T>(rawValue);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Converts a raw value into the requested type.
        /// Supports:
        /// - direct typed values
        /// - null values
        /// - JSON-backed values restored as <see cref="JsonElement"/>
        /// - simple convertible primitives
        /// </summary>
        private static T? ConvertValue<T>(object? rawValue)
        {
            if (rawValue is null)
                return default;

            if (rawValue is T typed)
                return typed;

            if (rawValue is JsonElement jsonElement)
            {
                if (typeof(T) == typeof(string) && jsonElement.ValueKind == JsonValueKind.String)
                {
                    object? value = jsonElement.GetString();
                    return (T?)value;
                }

                return jsonElement.Deserialize<T>();
            }

            return (T?)Convert.ChangeType(rawValue, typeof(T));
        }
    }
}