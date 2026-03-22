using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Events.Runtime
{
    /// <summary>
    /// Generic structured runtime log event.
    ///
    /// Typically used to stream live diagnostic logs to the frontend console
    /// through a realtime provider.
    /// </summary>
    [RealtimeEvent("runtime-log")]
    public sealed class RuntimeLogEvent : IRuntimeEvent
    {
        /// <summary>
        /// Log level (Information, Warning, Error, etc.).
        /// </summary>
        public required string Level { get; init; }

        /// <summary>
        /// Main log message.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Optional category such as Runtime, Authorization, Middleware, Redis, etc.
        /// </summary>
        public string? Category { get; init; }

        /// <summary>
        /// Optional user id associated with the log event.
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// Optional structured payload attached to the log entry.
        /// </summary>
        public object? Data { get; init; }

        /// <summary>
        /// Dynamic routing target for realtime delivery.
        /// Example: a specific console group or a single connection.
        /// </summary>
        public required RealtimeTarget RealtimeTarget { get; init; }

        /// <summary>
        /// UTC timestamp when the event was produced.
        /// </summary>
        public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}
