using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Provides the default retry policy definition resolution from step configuration.
    /// </summary>
    /// <remarks>
    /// This resolver reads retry configuration from <c>config.retry</c> and converts it
    /// into a strongly typed <see cref="AiRetryPolicyDefinition"/>.
    /// </remarks>
    public sealed class DefaultAiRetryPolicyDefinitionResolver : IAiRetryPolicyDefinitionResolver
    {
        /// <inheritdoc />
        public AiRetryPolicyDefinition? Resolve(IReadOnlyDictionary<string, object?> config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!config.TryGetValue("retry", out var retryValue) || retryValue is null)
            {
                return null;
            }

            var retryConfig = AsDictionary(retryValue);
            if (retryConfig is null)
            {
                return null;
            }

            var policies = ResolvePolicies(retryConfig);
            if (policies.Count == 0)
            {
                return null;
            }

            return new AiRetryPolicyDefinition
            {
                Policies = policies,
                MaxRetries = GetInt32(retryConfig, "maxRetries", 3),
                Strategy = GetBackoffStrategy(retryConfig, "strategy", AiRetryBackoffStrategy.Fixed),
                BaseDelayMs = GetInt32(retryConfig, "baseDelayMs", 500),
                MaxDelayMs = GetNullableInt32(retryConfig, "maxDelayMs"),
                Jitter = GetBoolean(retryConfig, "jitter", false)
            };
        }

        private static IReadOnlyDictionary<string, object?>? AsDictionary(object value)
        {
            if (value is IReadOnlyDictionary<string, object?> ro)
            {
                return ro;
            }

            if (value is IDictionary<string, object?> dict)
            {
                return new Dictionary<string, object?>(dict);
            }

            return null;
        }

        private static IReadOnlyList<string> ResolvePolicies(IReadOnlyDictionary<string, object?> retryConfig)
        {
            if (retryConfig.TryGetValue("policies", out var policiesValue) && policiesValue is not null)
            {
                return AsStringList(policiesValue);
            }

            if (retryConfig.TryGetValue("policy", out var policyValue) && policyValue is not null)
            {
                var policy = Convert.ToString(policyValue);

                return string.IsNullOrWhiteSpace(policy)
                    ? Array.Empty<string>()
                    : new[] { policy };
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> AsStringList(object value)
        {
            if (value is string single)
            {
                return string.IsNullOrWhiteSpace(single)
                    ? Array.Empty<string>()
                    : new[] { single };
            }

            if (value is IEnumerable enumerable)
            {
                return enumerable
                    .Cast<object?>()
                    .Select(Convert.ToString)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        private static int GetInt32(
            IReadOnlyDictionary<string, object?> values,
            string key,
            int defaultValue)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value);
        }

        private static int? GetNullableInt32(
            IReadOnlyDictionary<string, object?> values,
            string key)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                return null;
            }

            return Convert.ToInt32(value);
        }

        private static bool GetBoolean(
            IReadOnlyDictionary<string, object?> values,
            string key,
            bool defaultValue)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                return defaultValue;
            }

            return Convert.ToBoolean(value);
        }

        private static AiRetryBackoffStrategy GetBackoffStrategy(
            IReadOnlyDictionary<string, object?> values,
            string key,
            AiRetryBackoffStrategy defaultValue)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                return defaultValue;
            }

            var text = Convert.ToString(value);

            return Enum.TryParse<AiRetryBackoffStrategy>(text, ignoreCase: true, out var strategy)
                ? strategy
                : defaultValue;
        }
    }
}