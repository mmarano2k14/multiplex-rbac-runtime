using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.Pipeline.Steps
{
    /// <summary>
    /// Shared context passed across all steps in the pipeline.
    /// Acts as a key-value store for data exchange between steps.
    /// </summary>
    public sealed class AiStepContext
    {
        /// <summary>
        /// Unique execution identifier for the pipeline run.
        /// </summary>
        public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Shared data exchanged between steps.
        /// </summary>
        public Dictionary<string, object?> Data { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Additional metadata used for diagnostics, orchestration, or transport concerns.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; } = new(StringComparer.Ordinal);

        public void Set<T>(string key, T value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            Data[key] = value;
        }

        public T? Get<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Data.TryGetValue(key, out var value))
                return default;

            if (value is null)
                return default;

            if (value is T typedValue)
                return typedValue;

            throw new InvalidCastException(
                $"Value under key '{key}' cannot be cast to '{typeof(T).Name}'.");
        }

        public bool TryGet<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!Data.TryGetValue(key, out var rawValue) || rawValue is null)
            {
                value = default;
                return false;
            }

            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            throw new InvalidCastException(
                $"Value under key '{key}' cannot be cast to '{typeof(T).Name}'.");
        }

        public bool Contains(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return Data.ContainsKey(key);
        }
    }
}