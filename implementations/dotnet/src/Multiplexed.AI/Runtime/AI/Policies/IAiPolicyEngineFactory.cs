using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Creates step-scoped AI policy engine instances.
    /// </summary>
    public interface IAiPolicyEngineFactory
    {
        /// <summary>
        /// Creates a new policy engine instance for the specified policy kind and step context.
        /// </summary>
        /// <param name="kind">The policy kind to handle.</param>
        /// <param name="stepContext">The current step execution context.</param>
        /// <returns>A new step-scoped policy engine instance.</returns>
        IAiPolicyEngine Create(
            AiPolicyKind kind,
            AiStepExecutionContext stepContext);

        /// <summary>
        /// Creates a new typed policy engine instance for the specified policy kind and step context.
        /// </summary>
        /// <typeparam name="TPolicyEngine">The expected policy engine contract.</typeparam>
        /// <param name="kind">The policy kind to handle.</param>
        /// <param name="stepContext">The current step execution context.</param>
        /// <returns>A new step-scoped typed policy engine instance.</returns>
        TPolicyEngine Create<TPolicyEngine>(
            AiPolicyKind kind,
            AiStepExecutionContext stepContext)
            where TPolicyEngine : class, IAiPolicyEngine;
    }
}