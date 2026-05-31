using Multiplexed.Abstractions.AI.ControlPlane.Observability.Area;
using Multiplexed.Abstractions.AI.Observability.Context;

namespace Multiplexed.Abstractions.AI.ControlPlane.Observability.Events
{
    /// <summary>
    /// Represents a structured control-plane observability event.
    ///
    /// This event is designed to be reused by replay, execution control,
    /// run control, instance registry, admission, shared queue,
    /// shared controller, and scaling components.
    /// </summary>
    public sealed class AiControlPlaneEvent
    {
        /// <summary>
        /// Generic control-plane event type.
        /// </summary>
        public required AiControlPlaneEventType EventType { get; init; }

        /// <summary>
        /// Logical control-plane area.
        /// </summary>
        public required AiControlPlaneArea Area { get; init; }

        /// <summary>
        /// Operation name inside the control-plane area.
        /// Examples: replay, audit, restore, pause, resume, cancel, admit-run.
        /// </summary>
        public required string Operation { get; init; }

        /// <summary>
        /// Operation outcome when known.
        /// </summary>
        public AiControlPlaneOperationOutcome? Outcome { get; init; }

        /// <summary>
        /// Shared runtime correlation context.
        /// This reuses the existing runtime observability context instead of
        /// creating a duplicate control-plane-specific context.
        /// </summary>
        public required AiRuntimeExecutionCorrelationContext Correlation { get; init; }

        /// <summary>
        /// UTC timestamp of the event.
        /// </summary>
        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Optional duration in milliseconds for completed operations.
        /// </summary>
        public long? DurationMs { get; init; }

        /// <summary>
        /// Optional human-readable message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Optional failure reason.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// Additional structured properties intended for logs, dashboards,
        /// metrics labels, trace attributes, and search indexes.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Properties { get; init; } =
            new Dictionary<string, object?>();
    }
}