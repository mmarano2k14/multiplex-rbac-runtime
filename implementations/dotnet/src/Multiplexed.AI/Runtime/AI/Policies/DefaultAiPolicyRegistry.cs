using System;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Provides the default in-memory registry for AI policies.
    /// </summary>
    /// <remarks>
    /// The registry indexes policies by key and resolves them by key, policy kind,
    /// or descriptor definitions. It is intended to be registered as a singleton
    /// because policy registrations are immutable after construction.
    /// </remarks>
    public sealed class DefaultAiPolicyRegistry : IAiPolicyRegistry
    {
        private readonly IReadOnlyDictionary<string, AiPolicyDescriptor> policiesByKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiPolicyRegistry"/> class.
        /// </summary>
        /// <param name="policies">The policies registered in the runtime container.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="policies"/> is <see langword="null"/>.
        /// </exception>
        public DefaultAiPolicyRegistry(IEnumerable<IAiPolicy> policies)
        {
            ArgumentNullException.ThrowIfNull(policies);

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
            ArgumentNullException.ThrowIfNull(keys);

            return keys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => Resolve(key, kind))
                .ToArray();
        }

        /// <inheritdoc />
        public IReadOnlyList<IAiPolicy> ResolveByKind(AiPolicyKind kind)
        {
            return policiesByKey.Values
                .Where(descriptor => descriptor.Kind == kind)
                .Select(descriptor => descriptor.Policy)
                .ToArray();
        }

        /// <inheritdoc />
        public IReadOnlyList<IAiPolicy> ResolveFromDefinitions(IEnumerable<AiPolicyDescriptor> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            return definitions
                .Select(definition => Resolve(definition.Key, definition.Kind))
                .ToArray();
        }

        /// <inheritdoc />
        public bool Exists(string key, AiPolicyKind kind)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return policiesByKey.TryGetValue(key, out var descriptor)
                && descriptor.Kind == kind;
        }

        /// <summary>
        /// Creates a policy descriptor from a registered policy instance.
        /// </summary>
        /// <param name="policy">The policy instance.</param>
        /// <returns>The created policy descriptor.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="policy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the policy does not expose a valid key.
        /// </exception>
        private static AiPolicyDescriptor CreateDescriptor(IAiPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

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