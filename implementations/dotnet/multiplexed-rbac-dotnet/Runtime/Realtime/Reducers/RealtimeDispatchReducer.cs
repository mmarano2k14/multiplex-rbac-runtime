using MultiplexedRbac.Runtime.Realtime.Events;
using MultiplexedRbac.Runtime.Realtime.Providers;
using MultiplexedRbac.Runtime.Realtime.Providers.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Reducers
{
    /// <summary>
    /// Generic reducer that forwards runtime events to the currently active
    /// realtime provider hosted by <see cref="IRealtimeProviderHost"/>.
    /// </summary>
    public sealed class RealtimeDispatchReducer<TEvent> : IRuntimeEventReducer<TEvent>
        where TEvent : class, IRuntimeEvent
    {
        private readonly IRealtimeProviderHost _providerHost;

        public RealtimeDispatchReducer(IRealtimeProviderHost providerHost)
        {
            _providerHost = providerHost ?? throw new ArgumentNullException(nameof(providerHost));
        }

        /// <summary>
        /// Publishes the runtime event using the active realtime provider.
        /// </summary>
        public Task ReduceAsync(TEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            return _providerHost.Provider.PublishAsync(@event, cancellationToken);
        }
    }
}
