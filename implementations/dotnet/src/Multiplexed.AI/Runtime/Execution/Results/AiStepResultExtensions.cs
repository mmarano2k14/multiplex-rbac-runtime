using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Steps;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Results
{
    /// <summary>
    /// Provides payload-safe helpers for reading step result data.
    ///
    /// PURPOSE:
    /// - Unifies access to step result data regardless of storage strategy
    ///   (inline Data vs external DataPayloads).
    /// - Ensures consistent deserialization and conversion behavior.
    ///
    /// DESIGN:
    /// - Payloads are resolved first when present.
    /// - Inline data is used as a fallback.
    /// - Conversion is handled transparently for caller simplicity.
    /// </summary>
    public static class AiStepResultExtensions
    {
        /// <summary>
        /// Gets an optional data value from the step result.
        ///
        /// BEHAVIOR:
        /// - Resolves from DataPayloads first if available.
        /// - Falls back to Data dictionary.
        /// - Attempts type conversion if needed.
        /// </summary>
        public static async Task<T?> GetDataAsync<T>(
            this AiStepResult result,
            string key,
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            object? raw = null;

            // Priority: payload storage
            if (result.DataPayloads != null &&
                result.DataPayloads.TryGetValue(key, out var payload))
            {
                raw = await resolver.ResolveAsync(payload, cancellationToken)
                    .ConfigureAwait(false);
            }
            // Fallback: inline data
            else if (result.Data != null &&
                     result.Data.TryGetValue(key, out var value))
            {
                raw = value;
            }

            if (raw is null)
            {
                return default;
            }

            if (raw is T typed)
            {
                return typed;
            }

            if (raw is JsonElement json)
            {
                return json.Deserialize<T>();
            }

            return JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(raw));
        }

        /// <summary>
        /// Gets a required data value from the step result.
        ///
        /// BEHAVIOR:
        /// - Same resolution rules as GetDataAsync
        /// - Throws if value is missing or cannot be converted
        /// </summary>
        public static async Task<T> GetRequiredDataAsync<T>(
            this AiStepResult result,
            string key,
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            var value = await result.GetDataAsync<T>(
                key,
                resolver,
                cancellationToken).ConfigureAwait(false);

            if (value is null)
            {
                throw new InvalidOperationException(
                    $"Required step result data '{key}' was not found or could not be converted to '{typeof(T).Name}'.");
            }

            return value;
        }
    }
}