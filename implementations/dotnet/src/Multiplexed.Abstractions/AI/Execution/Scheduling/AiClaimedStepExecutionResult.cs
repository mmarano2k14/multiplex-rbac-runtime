using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Represents the execution result of a claimed DAG step.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Preserves the explicit relationship between a distributed DAG claim
    ///   and its execution result.
    /// - Avoids relying on positional ordering or step-name reconstruction.
    /// - Allows batch orchestration to remain deterministic and distributed-safe.
    ///
    /// IMPORTANT:
    /// - The associated <see cref="AiClaimedStep"/> remains the authoritative
    ///   distributed ownership descriptor.
    /// - The associated <see cref="AiStepResult"/> only represents
    ///   the execution outcome.
    /// </remarks>
    public sealed class AiClaimedStepExecutionResult
    {
        /// <summary>
        /// Gets or sets the claimed DAG step associated with this execution result.
        /// </summary>
        public required AiClaimedStep ClaimedStep { get; init; }

        /// <summary>
        /// Gets or sets the execution result produced by the claimed step.
        /// </summary>
        public required AiStepResult Result { get; init; }
    }
}