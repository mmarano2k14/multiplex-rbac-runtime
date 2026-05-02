using System;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Provides the default in-memory registry for AI policies.
    /// </summary>
    public sealed class DefaultAiPolicyRegistry : IAiPolicyRegistry
    {
        private readonly IReadOnlyDictionary<string, AiPolicyDescriptor> policiesByKey;

        public DefaultAiPolicyRegistry(IEnumerable<IAiPolicy> policies)
        {
            if (policies is null)
            {
                throw new ArgumentNullException(nameof(policies));
            }

            policiesByKey = policies
                .Select(CreateDescriptor)
                .ToDictionary(
                    descriptor => descriptor.Key,
                    descriptor => descriptor,
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public IAiPolicy Resolve(string key, AiPolicyKind kind)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Policy key cannot be null, empty, or whitespace.", nameof(key));
            }

            if (!policiesByKey.TryGetValue(key, out var descriptor))
            {
                throw new InvalidOperationException($"No AI policy is registered with key '{key}'.");
            }

            if (descriptor.Kind != kind)
            {
                throw new InvalidOperationException(
                    $"AI policy '{key}' is registered as '{descriptor.Kind}' but was requested as '{kind}'.");
            }

            return descriptor.Policy;
        }

        /// <inheritdoc />
        public IReadOnlyList<IAiPolicy> ResolveMany(IEnumerable<string> keys, AiPolicyKind kind)
        {
            if (keys is null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            return keys.Select(key => Resolve(key, kind)).ToArray();
        }

        private static AiPolicyDescriptor CreateDescriptor(IAiPolicy policy)
        {
            if (policy is null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            var attribute = policy.GetType()
                .GetCustomAttributes(typeof(AiPolicyAttribute), inherit: false)
                .OfType<AiPolicyAttribute>()
                .SingleOrDefault();

            var key = attribute?.Key ?? policy.Key;
            var kind = attribute?.Kind ?? policy.Kind;

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    $"AI policy type '{policy.GetType().FullName}' has no valid policy key.");
            }

            return new AiPolicyDescriptor(key, kind, policy);
        }
    }
}