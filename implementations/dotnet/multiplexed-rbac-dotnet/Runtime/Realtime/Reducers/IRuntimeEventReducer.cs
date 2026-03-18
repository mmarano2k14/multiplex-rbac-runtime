using MultiplexedRbac.Runtime.Realtime.Events;

namespace MultiplexedRbac.Runtime.Realtime.Reducers
{
    /// <summary>
    /// Defines a reducer capable of reacting to a runtime event.
    /// </summary>
    public interface IRuntimeEventReducer<in TEvent>
        where TEvent : class, IRuntimeEvent
    {
        /// <summary>
        /// Processes the specified runtime event.
        /// </summary>
        Task ReduceAsync(TEvent @event, CancellationToken cancellationToken = default);
    }
}
