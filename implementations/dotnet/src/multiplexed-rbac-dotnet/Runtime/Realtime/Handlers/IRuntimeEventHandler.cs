using MultiplexedRbac.Runtime.Realtime.Events;

namespace MultiplexedRbac.Runtime.Realtime.Handlers
{
    /// <summary>
    /// Defines a handler capable of reacting to a runtime event.
    /// </summary>
    public interface IRuntimeEventHandler<in TEvent>
        where TEvent : class, IRuntimeEvent
    {
        /// <summary>
        /// Handles the specified runtime event.
        /// </summary>
        Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
    }
}