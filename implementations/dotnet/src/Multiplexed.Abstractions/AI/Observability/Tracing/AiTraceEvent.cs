namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Represents a single trace event captured during AI runtime execution.
    /// </summary>
    public sealed class AiTraceEvent
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the timestamp of the event in UTC.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the logical category of the event.
        /// Example: Execution, Step, Storage, Retention.
        /// </summary>
        public string Category { get; set; } = default!;

        /// <summary>
        /// Gets or sets the event name.
        /// Example: StepStarted, StepCompleted, ClaimAcquired.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets an optional step identifier.
        /// </summary>
        public string? StepId { get; set; }

        /// <summary>
        /// Gets or sets additional structured tags associated with the event.
        /// </summary>
        public IDictionary<string, object?> Tags { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets the runtime trace correlation snapshot attached to this trace event.
        /// </summary>
        public AiRuntimeTraceCorrelationContext? Correlation { get; set; }
    }
}