using System.Collections.Generic;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Provides runtime access to registered AI policies and supports
    /// resolution by key, policy kind, and policy definitions.
    /// </summary>
    public interface IAiPolicyRegistry
    {
        /// <summary>
        /// Resolves a policy by key and expected policy kind.
        /// </summary>
        /// <param name="key">The unique policy key.</param>
        /// <param name="kind">The expected policy kind.</param>
        /// <returns>The resolved policy.</returns>
        IAiPolicy Resolve(string key, AiPolicyKind kind);

        /// <summary>
        /// Resolves multiple policies by key and expected policy kind.
        /// </summary>
        /// <param name="keys">The policy keys.</param>
        /// <param name="kind">The expected policy kind.</param>
        /// <returns>The resolved policies.</returns>
        IReadOnlyList<IAiPolicy> ResolveMany(IEnumerable<string> keys, AiPolicyKind kind);

        /// <summary>
        /// Resolves all policies registered for the specified policy kind.
        /// </summary>
        /// <param name="kind">The policy kind.</param>
        /// <returns>The matching policies.</returns>
        IReadOnlyList<IAiPolicy> ResolveByKind(AiPolicyKind kind);

        /// <summary>
        /// Resolves policies from a set of policy definitions.
        /// </summary>
        /// <param name="definitions">The policy definitions.</param>
        /// <returns>The resolved policies.</returns>
        IReadOnlyList<IAiPolicy> ResolveFromDefinitions(IEnumerable<AiPolicyDescriptor> definitions);

        /// <summary>
        /// Determines whether a policy exists for the given key and kind.
        /// </summary>
        /// <param name="key">The policy key.</param>
        /// <param name="kind">The policy kind.</param>
        /// <returns><see langword="true"/> if the policy exists; otherwise, <see langword="false"/>.</returns>
        bool Exists(string key, AiPolicyKind kind);
    }
}