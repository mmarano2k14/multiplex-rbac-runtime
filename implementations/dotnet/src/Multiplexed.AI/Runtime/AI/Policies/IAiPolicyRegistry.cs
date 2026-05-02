using System.Collections.Generic;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Provides runtime access to registered AI policies by key and policy kind.
    /// </summary>
    public interface IAiPolicyRegistry
    {
        /// <summary>
        /// Resolves a policy by key and expected policy kind.
        /// </summary>
        IAiPolicy Resolve(string key, AiPolicyKind kind);

        /// <summary>
        /// Resolves multiple policies by key and expected policy kind.
        /// </summary>
        IReadOnlyList<IAiPolicy> ResolveMany(IEnumerable<string> keys, AiPolicyKind kind);
    }
}