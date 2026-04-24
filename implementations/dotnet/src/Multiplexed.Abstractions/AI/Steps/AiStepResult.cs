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

        public bool Success { get; set; }

        public object? Value { get; set; }

        public AiStoredPayload? Payload { get; set; }

        public string? Output { get; set; }

        public string? Error { get; set; }

        /// <summary>
        /// Structured data returned by the step.
        /// This acts as an extension payload for additional outputs.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Optional payload-backed representation of structured data entries.
        ///
        /// PURPOSE:
        /// - Allows large individual Data entries to be externalized without removing
        ///   the existing inline Data dictionary.
        ///
        /// COMPATIBILITY:
        /// - Existing callers can continue reading <see cref="Data"/>.
        /// - Payload-aware callers should use <see cref="GetDataAsync{T}"/>.
        /// - When a key exists in both dictionaries, <see cref="DataPayloads"/> takes priority.
        /// </summary>
        public Dictionary<string, AiStoredPayload>? DataPayloads { get; set; }

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

        public T? GetValue<T>()
        {
            return ConvertValue<T>(Value);
        }

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

        public T? GetData<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Data.TryGetValue(key, out var rawValue))
                return default;

            return ConvertValue<T>(rawValue);
        }

        /// <summary>
        /// Retrieves a structured data entry using payload resolution when available.
        ///
        /// BEHAVIOR:
        /// - DataPayloads has priority over inline Data.
        /// - Falls back to inline Data for backward compatibility.
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

            return GetData<T>(key);
        }

        /// <summary>
        /// Synchronous compatibility helper for payload-aware structured data access.
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

            return GetData<T>(key);
        }

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
        /// Stores or replaces a payload-backed structured data entry.
        ///
        /// IMPORTANT:
        /// - Does not remove the inline Data entry.
        /// - Payload-aware accessors will prefer this entry.
        /// </summary>
        public void SetDataPayload(string key, AiStoredPayload payload)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(payload);

            DataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);
            DataPayloads[key] = payload;
        }

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