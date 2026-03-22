using Multiplexed.Realtime.Events;
using Multiplexed.Realtime.Events.Abstractions;
    
namespace MultiplexedRbac.Runtime.Realtime.Events.Runtime
{
    /// <summary>
    /// Event emitted when an access context key is rotated.
    ///
    /// This event can be consumed by reducers for logging, realtime propagation,
    /// metrics, or future audit pipelines.
    /// </summary>
    [RealtimeEvent("context-rotated")]
    public sealed class ContextRotatedEvent : IRuntimeEvent
    {
        /// <summary>
        /// Identifier of the user whose context was rotated.
        /// </summary>
        public required string UserId { get; init; }

        /// <summary>
        /// Previous context key before rotation.
        /// </summary>
        public required string OldContextKey { get; init; }

        /// <summary>
        /// Newly issued context key after rotation.
        /// </summary>
        public required string NewContextKey { get; init; }

        /// <summary>
        /// Dynamic routing target for realtime delivery.
        /// In most cases, a context rotation should be sent only to the affected user.
        /// </summary>
        public required RealtimeTarget RealtimeTarget { get; init; }

        /// <summary>
        /// UTC timestamp when the event occurred.
        /// </summary>
        public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}
