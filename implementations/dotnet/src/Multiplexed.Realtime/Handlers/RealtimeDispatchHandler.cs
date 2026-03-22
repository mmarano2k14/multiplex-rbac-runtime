using Multiplexed.Realtime.Abstractions;
using Multiplexed.Realtime.Events;

namespace Multiplexed.Realtime.Handlers
{
    /// <summary>
    /// Generic handler that forwards runtime events to the currently active
    /// realtime transport hosted by <see cref="IRealtimeTransportHost"/>.
    /// </summary>
    public sealed class RealtimeDispatchHandler<TEvent> : IRuntimeEventHandler<TEvent>
        where TEvent : class, IRuntimeEvent
    {
        private readonly IRealtimeTransportHost _transportHost;

        public RealtimeDispatchHandler(IRealtimeTransportHost transportHost)
        {
            _transportHost = transportHost ?? throw new ArgumentNullException(nameof(transportHost));
        }

        /// <summary>
        /// Publishes the runtime event using the active realtime transport.
        /// </summary>
        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            return _transportHost.Transport.PublishAsync(@event, cancellationToken);
        }
    }
}