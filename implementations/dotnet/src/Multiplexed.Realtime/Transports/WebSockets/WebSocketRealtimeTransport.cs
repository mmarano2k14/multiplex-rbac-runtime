using Multiplexed.Realtime.Events.Abstractions;
using Multiplexed.Realtime.Events;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using System.Reflection;
using Multiplexed.Realtime.Abstractions;
using Microsoft.AspNetCore.Builder;

namespace Multiplexed.Realtime.Transports.WebSockets
{
    /// <summary>
    /// WebSocket-based realtime provider.
    ///
    /// Publishes runtime events to connected WebSocket clients using the
    /// routing rules defined by RealtimeTarget.
    /// </summary>
    public sealed class WebSocketRealtimeTransport : IRealtimeTransport, IRealtimeEndpointMapper
    {
        private readonly WebSocketClientRegistry _registry;
        private static readonly ConcurrentDictionary<Type, string> EventNameCache = new();
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public WebSocketRealtimeTransport(WebSocketClientRegistry registry)
        {
            _registry = registry;
        }

        public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class, IRuntimeEvent
        {
            ArgumentNullException.ThrowIfNull(@event);

            var eventName = ResolveEventName(@event.GetType());

            var envelope = new
            {
                type = eventName,
                payload = @event
            };

            var json = JsonSerializer.Serialize(envelope, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            var targets = ResolveTargets(@event.RealtimeTarget);

            foreach (var connection in targets)
            {
                if (connection.Socket.State != System.Net.WebSockets.WebSocketState.Open)
                    continue;

                await connection.Socket.SendAsync(
                    segment,
                    System.Net.WebSockets.WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
            }
        }

        public IEndpointConventionBuilder? MapEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, string path)
        {
            WebSocketRealtimeEndpoint.Map(endpoints, path);
            return null;
        }

        private IReadOnlyCollection<WebSocketClientConnection> ResolveTargets(RealtimeTarget target)
        {
            return target.Kind switch
            {
                RealtimeTargetKind.All => _registry.GetAll(),
                RealtimeTargetKind.User => _registry.GetByUser(target.Value!),
                RealtimeTargetKind.Group => _registry.GetByGroup(target.Value!),
                RealtimeTargetKind.Connection => _registry.GetByConnectionId(target.Value!) is { } c
                    ? new[] { c }
                    : Array.Empty<WebSocketClientConnection>(),
                _ => Array.Empty<WebSocketClientConnection>()
            };
        }

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
