using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Defines the common contract for step-scoped AI policy engines.
    /// </summary>
    /// <remarks>
    /// Policy engines are created for a specific <see cref="AiStepExecutionContext"/>
    /// and should not be reused across unrelated step executions.
    /// </remarks>
    public interface IAiPolicyEngine
    {
        /// <summary>
        /// Gets the policy kind handled by the engine.
        /// </summary>
        AiPolicyKind Kind { get; }

        /// <summary>
        /// Gets the step execution context bound to this policy engine instance.
        /// </summary>
        AiStepExecutionContext StepContext { get; }
    }
}