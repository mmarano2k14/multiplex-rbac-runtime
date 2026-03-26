using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Resolves runtime AI step implementations from declarative step keys.
    /// This registry allows provider-neutral pipeline definitions to remain
    /// portable while still enabling the runtime to resolve concrete step instances.
    /// </summary>
    public interface IAiStepRegistry
    {
        /// <summary>
        /// Resolves the runtime step instance for the specified declarative step key.
        /// </summary>
        /// <param name="stepKey">The unique declarative step key.</param>
        /// <returns>The matching runtime step instance.</returns>
        IAiStep Resolve(string stepKey);
    }
}