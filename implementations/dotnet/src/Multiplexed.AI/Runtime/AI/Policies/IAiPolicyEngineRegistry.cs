using System;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Provides cached runtime access to AI policy engine implementation types.
    /// </summary>
    public interface IAiPolicyEngineRegistry
    {
        /// <summary>
        /// Resolves the engine implementation type registered for the specified policy kind.
        /// </summary>
        /// <param name="kind">The policy kind.</param>
        /// <returns>The registered engine implementation type.</returns>
        Type Resolve(AiPolicyKind kind);

        /// <summary>
        /// Determines whether an engine is registered for the specified policy kind.
        /// </summary>
        /// <param name="kind">The policy kind.</param>
        /// <returns><see langword="true"/> if an engine is registered; otherwise, <see langword="false"/>.</returns>
        bool Exists(AiPolicyKind kind);
    }
}