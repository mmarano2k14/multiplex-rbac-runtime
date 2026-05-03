using System;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Marks an AI policy engine as the handler for a specific policy kind.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AiPolicyEngineAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiPolicyEngineAttribute"/> class.
        /// </summary>
        /// <param name="kind">The policy kind handled by the engine.</param>
        public AiPolicyEngineAttribute(AiPolicyKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// Gets the policy kind handled by the engine.
        /// </summary>
        public AiPolicyKind Kind { get; }
    }
}