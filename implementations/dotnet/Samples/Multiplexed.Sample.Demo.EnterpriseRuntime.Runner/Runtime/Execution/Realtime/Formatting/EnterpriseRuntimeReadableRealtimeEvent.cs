namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Realtime.Formatting
{
    /// <summary>
    /// Represents a readable runtime realtime event for console output.
    /// </summary>
    public sealed class EnterpriseRuntimeReadableRealtimeEvent
    {
        /// <summary>
        /// Gets or initializes the readable realtime event kind.
        /// </summary>
        public required EnterpriseRuntimeRealtimeEventKind Kind { get; init; }

        /// <summary>
        /// Gets or initializes the runtime event level.
        /// </summary>
        public required string Level { get; init; }

        /// <summary>
        /// Gets or initializes the runtime event category.
        /// </summary>
        public required string Category { get; init; }

        /// <summary>
        /// Gets or initializes the original runtime event message.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Gets or initializes the event occurrence time.
        /// </summary>
        public DateTimeOffset? OccurredAtUtc { get; init; }

        /// <summary>
        /// Gets or initializes the execution identifier.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Gets or initializes the step name.
        /// </summary>
        public string? StepName { get; init; }

        /// <summary>
        /// Gets or initializes the runtime worker identifier.
        /// </summary>
        public string? WorkerId { get; init; }

        /// <summary>
        /// Gets or initializes the step claim token.
        /// </summary>
        public string? ClaimToken { get; init; }

        /// <summary>
        /// Gets or initializes the execution or step status.
        /// </summary>
        public string? Status { get; init; }

        /// <summary>
        /// Gets or initializes the error message.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Gets or initializes a normalized source signature for diagnostics.
        /// </summary>
        public string? SourceSignature { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether this event is verbose noise.
        /// </summary>
        public bool IsNoise { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether this event is important.
        /// </summary>
        public bool IsImportant { get; init; }
    }
}