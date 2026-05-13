using Multiplexed.Abstractions.AI.Concurrency;

namespace Multiplexed.AI.Runtime.Execution.Engine.Helpers
{
    /// <summary>
    /// Represents the effective concurrency admission data for a DAG step.
    /// </summary>
    public sealed class AiDagConcurrencyAdmission
    {
        /// <summary>
        /// Gets the concurrency context used for policy evaluation and Redis admission.
        /// </summary>
        public required AiConcurrencyContext Context { get; init; }

        /// <summary>
        /// Gets the effective concurrency definition after applying matching throttle rules.
        /// </summary>
        public required AiConcurrencyDefinition Definition { get; init; }
    }
}