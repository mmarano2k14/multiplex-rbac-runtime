using System.Text.Json;
using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Concurrency.Policies
{
    /// <summary>
    /// Provides provider/model-level admission control for concurrency policy evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy evaluates the provider/model pair carried by
    /// <see cref="AiConcurrencyContext.Provider"/> and <see cref="AiConcurrencyContext.Model"/>
    /// before the runtime attempts distributed Redis concurrency-slot acquisition.
    /// </para>
    ///
    /// <para>
    /// Models are evaluated using the normalized key format:
    /// </para>
    ///
    /// <code>
    /// {provider}:{model}
    /// </code>
    ///
    /// <para>
    /// Example configuration:
    /// </para>
    ///
    /// <code>
    /// {
    ///   "name": "concurrency.model.admission",
    ///   "config": {
    ///     "allowedModels": [ "openai:gpt-4.1", "openai:gpt-4o" ],
    ///     "blockedModels": [ "openai:gpt-3.5" ],
    ///     "requireModel": true,
    ///     "retryAfterMs": 500,
    ///     "reason": "Model is not allowed for this concurrency policy."
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [AiPolicy("concurrency.model.admission", Kind = AiPolicyKind.Concurrency)]
    public sealed class AiModelAdmissionConcurrencyPolicy
        : AiPolicyBase<AiConcurrencyPolicyContext>
    {
        /// <inheritdoc />
        public override string Key => "concurrency.model.admission";

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
            var model = Normalize(context.Concurrency.Model);
            var providerModel = CreateProviderModelKey(provider, model);

            if (string.IsNullOrWhiteSpace(model))
            {
                if (ReadBool(config, "requireModel") == true)
                {
                    return Task.FromResult(Deny(
                        config,
                        "Concurrency model is required but was not provided."));
                }

                return Task.FromResult(Allow());
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                if (ReadBool(config, "requireProvider") == true)
                {
                    return Task.FromResult(Deny(
                        config,
                        "Concurrency provider is required for model admission but was not provided."));
                }

                return Task.FromResult(Allow());
            }

            var blockedModels = ReadStringSet(
                config,
                "blockedModels");

            if (blockedModels.Contains(providerModel))
            {
                return Task.FromResult(Deny(
                    config,
                    $"Model '{context.Concurrency.Provider}:{context.Concurrency.Model}' is blocked by concurrency policy."));
            }

            var allowedModels = ReadStringSet(
                config,
                "allowedModels");

            if (allowedModels.Count > 0 && !allowedModels.Contains(providerModel))
            {
                return Task.FromResult(Deny(
                    config,
                    $"Model '{context.Concurrency.Provider}:{context.Concurrency.Model}' is not allowed by concurrency policy."));
            }

            return Task.FromResult(Allow());
        }

        /// <summary>
        /// Creates an allowed concurrency policy result.
        /// </summary>
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
        /// Creates the normalized provider/model key.
        /// </summary>
        private static string CreateProviderModelKey(
            string provider,
            string model)
        {
            return string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model)
                ? string.Empty
                : $"{provider}:{model}";
        }

        /// <summary>
        /// Reads a string set from policy configuration.
        /// </summary>
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
        /// Normalizes provider and model values for case-insensitive comparison.
        /// </summary>
        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}