using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Events
{
    /// <summary>
    /// Base contract for all runtime events that can be dispatched and published
    /// through realtime providers.
    ///
    /// Each event instance provides:
    /// - a UTC timestamp,
    /// - a realtime routing target.
    ///
    /// The public event name is defined by the EventNameAttribute on the type.
    /// </summary>
    public interface IRuntimeEvent
    {
        /// <summary>
        /// UTC timestamp indicating when the event was produced.
        /// </summary>
        DateTimeOffset OccurredAtUtc { get; }

        /// <summary>
        /// Dynamic realtime target used by the provider to route the event.
        /// </summary>
        RealtimeTarget RealtimeTarget { get; }
    }
}
