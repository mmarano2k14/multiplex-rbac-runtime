using MultiplexedRbac.Runtime.Realtime.Events;
using MultiplexedRbac.Runtime.Realtime.Providers.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Providers
{
    /// <summary>
    /// Default no-op realtime provider.
    ///
    /// Used when realtime transport is disabled or not configured.
    /// This allows the runtime to keep the same event pipeline without
    /// requiring conditional checks everywhere.
    /// </summary>
    public sealed class NullRealtimeProvider : IRealtimeProvider
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
