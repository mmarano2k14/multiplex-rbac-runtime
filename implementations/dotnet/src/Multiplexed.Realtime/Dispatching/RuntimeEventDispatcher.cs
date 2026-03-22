using Microsoft.Extensions.Logging;
using Multiplexed.Realtime.Events;
using System.Threading.Channels;

namespace Multiplexed.Realtime.Dispatching
{
    /// <summary>
    /// Fast, non-blocking runtime event dispatcher.
    ///
    /// This dispatcher only enqueues events into a bounded channel.
    /// Actual reducer execution happens in a background worker.
    /// </summary>
    public sealed class RuntimeEventDispatcher : IRuntimeEventDispatcher
    {
        private readonly ChannelWriter<IRuntimeEvent> _writer;
        private readonly ILogger<RuntimeEventDispatcher> _logger;

        public RuntimeEventDispatcher(
            Channel<IRuntimeEvent> channel,
            ILogger<RuntimeEventDispatcher> logger)
        {
            _writer = channel.Writer;
            _logger = logger;
        }

        /// <summary>
        /// Attempts to enqueue the event without blocking the caller.
        /// If the queue is full, the event is dropped intentionally.
        /// </summary>
        public void Dispatch(IRuntimeEvent runtimeEvent)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);

            if (!_writer.TryWrite(runtimeEvent))
            {
                // This should never break the hot path.
                // Dropping observability/realtime events is preferable
                // to blocking request execution.
                _logger.LogDebug(
                    "Runtime event queue is full. Event of type {EventType} was dropped.",
                    runtimeEvent.GetType().FullName);
            }
        }
    }
}

