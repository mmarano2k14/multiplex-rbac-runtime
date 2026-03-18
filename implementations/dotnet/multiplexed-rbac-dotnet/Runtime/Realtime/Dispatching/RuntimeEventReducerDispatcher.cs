using MultiplexedRbac.Runtime.Realtime.Events;
using System.Reflection;

namespace MultiplexedRbac.Runtime.Realtime.Dispatching
{
    /// <summary>
    /// Resolves and executes reducers for the concrete runtime event type.
    /// </summary>
    public sealed class RuntimeEventReducerDispatcher : IRuntimeEventReducerDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public RuntimeEventReducerDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task DispatchAsync(IRuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);

            var eventType = runtimeEvent.GetType();
            var reducerInterfaceType = typeof(Reducers.IRuntimeEventReducer<>).MakeGenericType(eventType);
            var reducers = _serviceProvider.GetServices(reducerInterfaceType);
            var reduceAsyncMethod = reducerInterfaceType.GetMethod("ReduceAsync", BindingFlags.Public | BindingFlags.Instance)!;

            foreach (var reducer in reducers)
            {
                var task = (Task)reduceAsyncMethod.Invoke(reducer, new object[] { runtimeEvent, cancellationToken })!;
                await task.ConfigureAwait(false);
            }
        }
    }
}
