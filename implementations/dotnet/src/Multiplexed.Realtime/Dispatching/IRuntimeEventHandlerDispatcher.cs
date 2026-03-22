using Multiplexed.Realtime.Events;

namespace Multiplexed.Realtime.Dispatching
{
    /// <summary>
    /// Dispatches a runtime event to all reducers registered for its concrete type.
    /// </summary>
    public interface IRuntimeEventHandlerDispatcher
    {
        /// <summary>
        /// Executes all reducers associated with the concrete runtime event type.
        /// </summary>
        Task DispatchAsync(IRuntimeEvent runtimeEvent, CancellationToken cancellationToken = default);
    }
}
