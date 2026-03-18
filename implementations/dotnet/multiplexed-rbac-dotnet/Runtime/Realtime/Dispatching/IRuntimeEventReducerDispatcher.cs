using MultiplexedRbac.Runtime.Realtime.Events;

namespace MultiplexedRbac.Runtime.Realtime.Dispatching
{
    /// <summary>
    /// Dispatches a runtime event to all reducers registered for its concrete type.
    /// </summary>
    public interface IRuntimeEventReducerDispatcher
    {
        /// <summary>
        /// Executes all reducers associated with the concrete runtime event type.
        /// </summary>
        Task DispatchAsync(IRuntimeEvent runtimeEvent, CancellationToken cancellationToken = default);
    }
}
