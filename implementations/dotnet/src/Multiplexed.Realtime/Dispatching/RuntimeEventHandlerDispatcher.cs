using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Realtime.Events;
using System.Reflection;

namespace Multiplexed.Realtime.Dispatching
{
    /// <summary>
    /// Resolves and executes handlers for the concrete runtime event type.
    /// </summary>
    public sealed class RuntimeEventHandlerDispatcher : IRuntimeEventHandlerDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public RuntimeEventHandlerDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Dispatches the specified runtime event to all registered handlers
        /// matching its concrete runtime type.
        /// </summary>
        public async Task DispatchAsync(IRuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);

            var eventType = runtimeEvent.GetType();
            var handlerInterfaceType = typeof(Handlers.IRuntimeEventHandler<>).MakeGenericType(eventType);
            var handlers = _serviceProvider.GetServices(handlerInterfaceType);
            var handleAsyncMethod = handlerInterfaceType.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)!;

            foreach (var handler in handlers)
            {
                var task = (Task)handleAsyncMethod.Invoke(handler, new object[] { runtimeEvent, cancellationToken })!;
                await task.ConfigureAwait(false);
            }
        }
    }
}