using System.Collections.Generic;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Resolves retry policy definitions from step configuration and runtime context values.
    /// </summary>
    /// <remarks>
    /// Implementations are responsible for reading retry configuration from a step configuration
    /// dictionary and resolving dynamic values such as state paths when supported by the runtime.
    /// </remarks>
    public interface IAiRetryPolicyDefinitionResolver
    {
        /// <summary>
        /// Resolves the retry policy definition for a step.
        /// </summary>
        /// <param name="config">The step configuration dictionary.</param>
        /// <returns>
        /// The resolved retry policy definition, or <c>null</c> when no retry configuration exists.
        /// </returns>
        AiRetryPolicyDefinition? Resolve(IReadOnlyDictionary<string, object?> config);
    }
}