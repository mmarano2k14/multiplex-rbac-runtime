using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.State;
using System.Globalization;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.State
{
    /// <summary>
    /// Default payload-aware reader for AI execution state values.
    ///
    /// PURPOSE:
    /// - Reads execution state values without mutating the state.
    /// - Resolves payload-backed data and metadata when available.
    /// - Falls back to inline state dictionaries when no payload exists.
    ///
    /// RULE:
    /// - Payload-backed values take precedence over inline values.
    /// - Inline values remain the backward-compatible fallback.
    /// </summary>
    public sealed class DefaultAiExecutionStateReader : IAiExecutionStateReader
    {
        private readonly IAiExecutionPayloadResolver _payloadResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionStateReader"/> class.
        /// </summary>
        public DefaultAiExecutionStateReader(IAiExecutionPayloadResolver payloadResolver)
        {
            _payloadResolver = payloadResolver ?? throw new ArgumentNullException(nameof(payloadResolver));
        }

        /// <inheritdoc />
        public async Task<T?> GetDataAsync<T>(
            AiExecutionState state,
            string key,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (state.DataPayloads != null &&
                state.DataPayloads.TryGetValue(key, out var payload))
            {
                var resolved = await _payloadResolver.ResolveAsync(payload, cancellationToken)
                    .ConfigureAwait(false);

                return ConvertValue<T>(resolved);
            }

            return TryReadInlineValue(
                state.Data,
                key,
                out T? value)
                ? value
                : default;
        }

        /// <inheritdoc />
        public async Task<T?> GetMetadataAsync<T>(
            AiExecutionState state,
            string key,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (state.MetadataPayloads != null &&
                state.MetadataPayloads.TryGetValue(key, out var payload))
            {
                var resolved = await _payloadResolver.ResolveAsync(payload, cancellationToken)
                    .ConfigureAwait(false);

                return ConvertValue<T>(resolved);
            }

            return TryReadInlineValue(
                state.Metadata,
                key,
                out T? value)
                ? value
                : default;
        }

        /// <summary>
        /// Attempts to read and convert an inline value from a dictionary.
        /// </summary>
        private static bool TryReadInlineValue<T>(
            IReadOnlyDictionary<string, object?> values,
            string key,
            out T? value)
        {
            if (!values.TryGetValue(key, out var raw))
            {
                value = default;
                return false;
            }

            value = ConvertValue<T>(raw);
            return true;
        }

        /// <summary>
        /// Converts a resolved or inline raw value to the requested target type.
        /// </summary>
        private static T? ConvertValue<T>(object? value)
        {
            if (value is null)
            {
                return default;
            }

            if (value is T typed)
            {
                return typed;
            }

            if (value is JsonElement json)
            {
                return json.Deserialize<T>();
            }

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (targetType == typeof(string))
            {
                return (T?)(object?)value.ToString();
            }

            return (T?)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
    }
}