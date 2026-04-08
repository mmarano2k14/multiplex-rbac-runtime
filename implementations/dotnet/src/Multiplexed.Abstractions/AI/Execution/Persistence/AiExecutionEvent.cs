using System;
using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Execution.Persistence
{
    /// <summary>
    /// Represents a technical event in the lifecycle of an AI execution.
    ///
    /// This is a lightweight append-only structure intended for debugging,
    /// audit, timeline reconstruction, and post-mortem inspection.
    ///
    /// It is not intended to be a full event sourcing model.
    /// </summary>
    public sealed class AiExecutionEvent
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the optional step name associated with the event.
        /// Null for execution-level events.
        /// </summary>
        public string? StepName { get; set; }

        /// <summary>
        /// Gets or sets the event type.
        /// Example values:
        /// ExecutionCreated
        /// StepClaimed
        /// StepCompleted
        /// StepFailed
        /// RetryScheduled
        /// StepRecovered
        /// ExecutionCompleted
        /// ExecutionFailed
        /// </summary>
        public string EventType { get; set; } = default!;

        /// <summary>
        /// Gets or sets the UTC timestamp when the event occurred.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets an optional human-readable message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets optional structured metadata associated with the event.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}