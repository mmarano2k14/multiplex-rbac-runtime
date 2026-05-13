using System.Text.Json;
using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Concurrency.Policies
{
    /// <summary>
    /// Provides provider-level admission control for concurrency policy evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy evaluates <see cref="AiConcurrencyContext.Provider"/> before the runtime attempts
    /// distributed Redis concurrency-slot acquisition.
    /// </para>
    ///
    /// <para>
    /// It can be used to require, allow, or block specific providers for a pipeline, step,
    /// environment, tenant, or runtime configuration.
    /// </para>
    ///
    /// <para>
    /// Example:
    /// </para>
    ///
    /// <code>
    /// {
    ///   "name": "concurrency.provider.admission",
    ///   "config": {
    ///     "allowedProviders": [ "openai", "anthropic" ],
    ///     "blockedProviders": [ "legacy-provider" ],
    ///     "requireProvider": true,
    ///     "retryAfterMs": 500,
    ///     "reason": "Provider is not allowed for this concurrency policy."
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [AiPolicy("concurrency.provider.admission", Kind = AiPolicyKind.Concurrency)]
    public sealed class AiProviderAdmissionConcurrencyPolicy
        : AiPolicyBase<AiConcurrencyPolicyContext>
    {
        /// <inheritdoc />
        public override string Key => "concurrency.provider.admission";

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
            var provider = Normalize(context.Concurrency.Provider);

            if (string.IsNullOrWhiteSpace(provider))
            {
                if (ReadBool(config, "requireProvider") == true)
                {
                    return Task.FromResult(Deny(
                        config,
                        "Concurrency provider is required but was not provided."));
                }

                return Task.FromResult(Allow());
            }

            var blockedProviders = ReadStringSet(
                config,
                "blockedProviders");

            if (blockedProviders.Contains(provider))
            {
                return Task.FromResult(Deny(
                    config,
                    $"Provider '{context.Concurrency.Provider}' is blocked by concurrency policy."));
            }

            var allowedProviders = ReadStringSet(
                config,
                "allowedProviders");

            if (allowedProviders.Count > 0 && !allowedProviders.Contains(provider))
            {
                return Task.FromResult(Deny(
                    config,
                    $"Provider '{context.Concurrency.Provider}' is not allowed by concurrency policy."));
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
        /// Normalizes provider values for case-insensitive comparison.
        /// </summary>
        /// <param name="value">
        /// The provider value.
        /// </param>
        /// <returns>
        /// The normalized provider value.
        /// </returns>
        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}