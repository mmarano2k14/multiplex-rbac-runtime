using MultiplexedRbac.Runtime.Realtime.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Events;

namespace MultiplexedRbac.Runtime.Realtime.Transports.Null
{
    /// <summary>
    /// Default no-op realtime provider.
    ///
    /// Used when realtime transport is disabled or not configured.
    /// This allows the runtime to keep the same event pipeline without
    /// requiring conditional checks everywhere.
    /// </summary>
    public sealed class NullRealtimeTransport : IRealtimeTransport
    {
        /// <summary>
        /// Ignores the runtime event.
        /// </summary>
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class, IRuntimeEvent
        {
            return Task.CompletedTask;
        }
    }
}
