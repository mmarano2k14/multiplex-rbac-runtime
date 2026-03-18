using MultiplexedRbac.Runtime.Realtime.Events;

namespace MultiplexedRbac.Runtime.Realtime.Dispatching
{
    /// <summary>
    /// Non-blocking dispatcher used by hot paths to enqueue runtime events
    /// for background processing.
    /// </summary>
    public interface IRuntimeEventDispatcher
    {
        /// <summary>
        /// Enqueues a runtime event for asynchronous background processing.
        /// </summary>
        void Dispatch(IRuntimeEvent runtimeEvent);
    }
}
