using System.Text.Json;
using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Concurrency.Policies
{
    /// <summary>
    /// Provides operation-level admission control for concurrency policy evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy evaluates <see cref="AiConcurrencyContext.Operation"/> before the runtime
    /// attempts distributed Redis concurrency-slot acquisition.
    /// </para>
    ///
    /// <para>
    /// It can be used to allow or block logical operations such as <c>llm.chat</c>,
    /// <c>embedding.create</c>, <c>rag.retrieve</c>, <c>vector.search</c>, or <c>tool.call</c>.
    /// </para>
    ///
    /// <para>
    /// Example:
    /// </para>
    ///
    /// <code>
    /// {
    ///   "name": "concurrency.operation.admission",
    ///   "config": {
    ///     "allowedOperations": [ "llm.chat", "rag.retrieve" ],
    ///     "blockedOperations": [ "tool.dangerous" ],
    ///     "requireOperation": true,
    ///     "retryAfterMs": 500,
    ///     "reason": "Operation is not allowed for this concurrency policy."
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [AiPolicy("concurrency.operation.admission", Kind = AiPolicyKind.Concurrency)]
    public sealed class AiOperationAdmissionConcurrencyPolicy
        : AiPolicyBase<AiConcurrencyPolicyContext>
    {
        /// <inheritdoc />
        public override string Key => "concurrency.operation.admission";

        /// <inheritdoc />
        public override AiPolicyKind Kind => AiPolicyKind.Concurrency;

        /// <inheritdoc />
        public override Task<AiPolicyResult> ExecuteAsync(
            AiConcurrencyPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            cancellationToken.ThrowIfCancellationRequested();

            var config = context.Config;
            var operation = Normalize(context.Concurrency.Operation);

            if (string.IsNullOrWhiteSpace(operation))
            {
                if (ReadBool(config, "requireOperation") == true)
                {
                    return Task.FromResult(Deny(
                        config,
                        "Concurrency operation is required but was not provided."));
                }

                return Task.FromResult(Allow());
            }

            var blockedOperations = ReadStringSet(
                config,
                "blockedOperations");

            if (blockedOperations.Contains(operation))
            {
                return Task.FromResult(Deny(
                    config,
                    $"Operation '{context.Concurrency.Operation}' is blocked by concurrency policy."));
            }

            var allowedOperations = ReadStringSet(
                config,
                "allowedOperations");

            if (allowedOperations.Count > 0 && !allowedOperations.Contains(operation))
            {
                return Task.FromResult(Deny(
                    config,
                    $"Operation '{context.Concurrency.Operation}' is not allowed by concurrency policy."));
            }

            return Task.FromResult(Allow());
        }

        /// <summary>
        /// Creates an allowed concurrency policy result.
        /// </summary>
        /// <returns>
        /// A successful policy result containing an allowed concurrency outcome.
        /// </returns>
        private static AiPolicyResult Allow()
        {
            return AiPolicyResult.Success(
                new AiConcurrencyPolicyOutcome
                {
                    IsAllowed = true
                });
        }

        /// <summary>
        /// Creates a denied concurrency policy outcome.
        /// </summary>
        /// <param name="config">
        /// The configured policy values.
        /// </param>
        /// <param name="fallbackReason">
        /// The fallback denial reason.
        /// </param>
        /// <returns>
        /// A successful policy result containing a denied concurrency outcome.
        /// </returns>
        private static AiPolicyResult Deny(
            IReadOnlyDictionary<string, object?> config,
            string fallbackReason)
        {
            var reason = ReadString(config, "reason") ?? fallbackReason;
            var retryAfterMs = ReadInt(config, "retryAfterMs");

            return AiPolicyResult.Success(
                new AiConcurrencyPolicyOutcome
                {
                    IsAllowed = false,
                    Reason = reason,
                    RetryAfter = retryAfterMs is > 0
                        ? TimeSpan.FromMilliseconds(retryAfterMs.Value)
                        : null
                });
        }

        /// <summary>
        /// Reads a string set from policy configuration.
        /// </summary>
        /// <param name="config">
        /// The policy configuration dictionary.
        /// </param>
        /// <param name="key">
        /// The configuration key.
        /// </param>
        /// <returns>
        /// A normalized, case-insensitive string set.
        /// </returns>
        private static HashSet<string> ReadStringSet(
            IReadOnlyDictionary<string, object?> config,
            string key)
        {
            if (!TryGetValue(config, key, out var value) || value is null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (value is IEnumerable<string> strings)
            {
                return strings
                    .Select(Normalize)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                return element
                    .EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => Normalize(x.GetString()))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            var single = Normalize(value.ToString());

            return string.IsNullOrWhiteSpace(single)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { single };
        }

        /// <summary>
        /// Reads a string value from policy configuration.
        /// </summary>
        /// <param name="config">
        /// The policy configuration dictionary.
        /// </param>
        /// <param name="key">
        /// The configuration key.
        /// </param>
        /// <returns>
        /// The configured string value, or <c>null</c> when missing.
        /// </returns>
        private static string? ReadString(
            IReadOnlyDictionary<string, object?> config,
            string key)
        {
            if (!TryGetValue(config, key, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                string text when !string.IsNullOrWhiteSpace(text) => text,
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Reads a boolean value from policy configuration.
        /// </summary>
        /// <param name="config">
        /// The policy configuration dictionary.
        /// </param>
        /// <param name="key">
        /// The configuration key.
        /// </param>
        /// <returns>
        /// The configured boolean value, or <c>null</c> when missing or invalid.
        /// </returns>
        private static bool? ReadBool(
            IReadOnlyDictionary<string, object?> config,
            string key)
        {
            if (!TryGetValue(config, key, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                bool boolean => boolean,
                string text when bool.TryParse(text, out var boolean) => boolean,
                JsonElement element when element.ValueKind == JsonValueKind.True => true,
                JsonElement element when element.ValueKind == JsonValueKind.False => false,
                JsonElement element when element.ValueKind == JsonValueKind.String &&
                                         bool.TryParse(element.GetString(), out var boolean) => boolean,
                _ => null
            };
        }

        /// <summary>
        /// Reads an integer value from policy configuration.
        /// </summary>
        /// <param name="config">
        /// The policy configuration dictionary.
        /// </param>
        /// <param name="key">
        /// The configuration key.
        /// </param>
        /// <returns>
        /// The configured integer value, or <c>null</c> when missing or invalid.
        /// </returns>
        private static int? ReadInt(
            IReadOnlyDictionary<string, object?> config,
            string key)
        {
            if (!TryGetValue(config, key, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                int number => number,
                long number when number is <= int.MaxValue and >= int.MinValue => (int)number,
                double number when number % 1 == 0 => (int)number,
                decimal number when number % 1 == 0 => (int)number,
                string text when int.TryParse(text, out var number) => number,
                JsonElement element when element.ValueKind == JsonValueKind.Number &&
                                         element.TryGetInt32(out var number) => number,
                JsonElement element when element.ValueKind == JsonValueKind.String &&
                                         int.TryParse(element.GetString(), out var number) => number,
                _ => null
            };
        }

        /// <summary>
        /// Attempts to read a value using case-insensitive key matching.
        /// </summary>
        /// <param name="config">
        /// The configuration dictionary.
        /// </param>
        /// <param name="key">
        /// The key to find.
        /// </param>
        /// <param name="value">
        /// The matched value.
        /// </param>
        /// <returns>
        /// <c>true</c> when the key exists; otherwise, <c>false</c>.
        /// </returns>
        private static bool TryGetValue(
            IReadOnlyDictionary<string, object?> config,
            string key,
            out object? value)
        {
            if (config.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (var pair in config)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Normalizes operation values for case-insensitive comparison.
        /// </summary>
        /// <param name="value">
        /// The operation value.
        /// </param>
        /// <returns>
        /// The normalized operation value.
        /// </returns>
        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}