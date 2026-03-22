using Multiplexed.Realtime.Events;

namespace Multiplexed.Realtime.Abstractions
{
    /// <summary>
    /// Represents a realtime transport capable of publishing runtime events
    /// to connected clients.
    ///
    /// Implementations may use SignalR, raw WebSockets, SSE, or any other
    /// realtime communication mechanism.
    /// </summary>
    public interface IRealtimeTransport
    {
        /// <summary>
        /// Publishes a runtime event through the underlying realtime transport.
        /// </summary>
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class, IRuntimeEvent;
    }
}
