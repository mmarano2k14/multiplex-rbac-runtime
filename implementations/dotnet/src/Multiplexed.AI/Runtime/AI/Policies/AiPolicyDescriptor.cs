using System;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Describes a registered AI policy implementation.
    /// </summary>
    public sealed class AiPolicyDescriptor
    {
        public AiPolicyDescriptor(string key, AiPolicyKind kind, IAiPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Policy key cannot be null, empty, or whitespace.", nameof(key));
            }

            Key = key;
            Kind = kind;
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>
        /// Gets the stable policy key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the policy kind.
        /// </summary>
        public AiPolicyKind Kind { get; }

        /// <summary>
        /// Gets the policy instance.
        /// </summary>
        public IAiPolicy Policy { get; }
    }
}