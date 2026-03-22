using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Multiplexed.Realtime.Abstractions;
using Multiplexed.Realtime.Events;
using Multiplexed.Realtime.Events.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;

namespace Multiplexed.Realtime.Transports.SignalR
{
    /// <summary>
    /// SignalR-based realtime provider.
    ///
    /// This provider publishes runtime events to SignalR clients and is also
    /// responsible for mapping the SignalR hub endpoint into ASP.NET routing.
    /// </summary>
    public sealed class SignalRRealtimeTransport : IRealtimeTransport, IRealtimeEndpointMapper
    {
        private readonly IHubContext<RealtimeHub> _hubContext;

        // Cache public realtime event names to avoid repeated reflection.
        private static readonly ConcurrentDictionary<Type, string> EventNameCache = new();

        public SignalRRealtimeTransport(IHubContext<RealtimeHub> hubContext)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        /// <summary>
        /// Publishes a runtime event to SignalR clients based on its declared
        /// public realtime name and routing target.
        /// </summary>
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class, IRuntimeEvent
        {
            ArgumentNullException.ThrowIfNull(@event);

            var eventName = ResolveEventName(@event.GetType());
            var target = @event.RealtimeTarget;

            return target.Kind switch
            {
                RealtimeTargetKind.All =>
                    _hubContext.Clients.All.SendAsync(eventName, @event, cancellationToken),

                RealtimeTargetKind.User =>
                    _hubContext.Clients.User(target.Value!).SendAsync(eventName, @event, cancellationToken),

                RealtimeTargetKind.Group =>
                    _hubContext.Clients.Group(target.Value!).SendAsync(eventName, @event, cancellationToken),

                RealtimeTargetKind.Connection =>
                    _hubContext.Clients.Client(target.Value!).SendAsync(eventName, @event, cancellationToken),

                _ => Task.CompletedTask
            };
        }

        /// <summary>
        /// Maps the SignalR hub endpoint to the provided runtime path.
        /// </summary>
        public IEndpointConventionBuilder MapEndpoints(IEndpointRouteBuilder endpoints, string path)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            return endpoints.MapHub<RealtimeHub>(path);
        }

        /// <summary>
        /// Resolves the public realtime event name declared on the event type.
        /// </summary>
        private static string ResolveEventName(Type eventType)
        {
            return EventNameCache.GetOrAdd(eventType, static type =>
            {
                var attribute = type.GetCustomAttribute<RealtimeEventAttribute>(inherit: false);

                if (attribute is null)
                {
                    throw new InvalidOperationException(
                        $"Realtime event '{type.FullName}' is missing the required RealtimeEventAttribute.");
                }

                return attribute.Name;
            });
        }
    }
}
