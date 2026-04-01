using Multiplexed.Abstractions.AI.Execution;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Represents a distributed-safe finalization request for a DAG execution.
    ///
    /// This model encapsulates all required data to atomically promote
    /// a global execution record into a terminal state.
    ///
    /// It is used by the DAG store (e.g., Redis Lua) to ensure:
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
        /// The finalization succeeds only if the persisted record matches this key.
        /// </summary>
        public string ExpectedExecutionStepKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target terminal status.
        /// </summary>
        public AiExecutionStatus Status { get; init; }

        /// <summary>
        /// Gets the list of completed step names at the time of convergence.
        /// </summary>
        public IReadOnlyList<string> CompletedSteps { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the current step name.
        ///
        /// Typically empty for terminal states.
        /// </summary>
        public string CurrentStep { get; init; } = string.Empty;

        /// <summary>
        /// Gets the identifier of the worker performing the finalization.
        ///
        /// This is optional and primarily used for diagnostics.
        /// </summary>
        public string? WorkerId { get; init; }
    }
}
