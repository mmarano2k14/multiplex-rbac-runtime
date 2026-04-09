using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Engine
{
    /// <summary>
    /// Represents a distributed-safe finalization request for a DAG execution.
    ///
    /// This model encapsulates all required data to atomically promote
    /// a global execution record into a terminal state.
    ///
    /// It is used by the DAG store (for example Redis Lua) to ensure:
    /// - optimistic concurrency via <see cref="ExpectedExecutionStepKey"/>
    /// - deterministic terminal state promotion
    /// - atomic update of execution metadata
    /// </summary>
    public sealed class AiDagExecutionFinalizationRequest
    {
        /// <summary>
        /// Gets the execution identifier.
        /// </summary>
        public string ExecutionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the expected execution step key used for optimistic concurrency control.
        ///
        /// Finalization succeeds only if the persisted record still matches this key.
        /// </summary>
        public string ExpectedExecutionStepKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target terminal execution status.
        ///
        /// Only terminal statuses should be used here, typically:
        /// - <see cref="AiExecutionStatus.Completed"/>
        /// - <see cref="AiExecutionStatus.Failed"/>
        /// - <see cref="AiExecutionStatus.Cancelled"/>
        /// </summary>
        public AiExecutionStatus Status { get; init; }

        /// <summary>
        /// Gets the UTC timestamp at which the execution reached its terminal state.
        /// </summary>
        public DateTime CompletedAtUtc { get; init; }

        /// <summary>
        /// Gets the list of completed step names at the time of convergence.
        /// </summary>
        public IReadOnlyList<string> CompletedSteps { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the current step name.
        ///
        /// This is typically empty for terminal states.
        /// </summary>
        public string CurrentStep { get; init; } = string.Empty;

        /// <summary>
        /// Gets the identifier of the worker performing the finalization.
        ///
        /// This value is optional and is primarily intended for diagnostics,
        /// observability, and concurrency analysis.
        /// </summary>
        public string? WorkerId { get; init; }
    }
}