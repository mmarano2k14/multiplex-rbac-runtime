using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Text;

namespace Multiplexed.Realtime.Transports.WebSockets
{
    /// <summary>
    /// Maps and handles the raw WebSocket realtime endpoint.
    /// </summary>
    public static class WebSocketRealtimeEndpoint
    {
        public static void Map(IEndpointRouteBuilder endpoints, string path)
        {
            endpoints.Map(path, HandleAsync);
        }

        private static async Task HandleAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var registry = context.RequestServices.GetRequiredService<WebSocketClientRegistry>();

            // NEED FIND A SOLUTION FOR HARDCODED userId
            var userId = context.User?.Identity?.IsAuthenticated == true
                ? context.User.Identity!.Name
                : context.Request.Query["userId"].ToString();

            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            var connectionId = registry.Add(socket, string.IsNullOrWhiteSpace(userId) ? null : userId);

            try
            {
                await ReceiveLoopAsync(socket, registry, connectionId, context.RequestAborted);
            }
            finally
            {
                registry.Remove(connectionId);
            }
        }

        private static async Task ReceiveLoopAsync(
            WebSocket socket,
            WebSocketClientRegistry registry,
            string connectionId,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested &&
                   socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed by client",
                        cancellationToken);

                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Minimal protocol for now:
                // "join:group-name"
                if (text.StartsWith("join:", StringComparison.OrdinalIgnoreCase))
                {
                    var groupName = text.Substring("join:".Length).Trim();

                    if (!string.IsNullOrWhiteSpace(groupName))
                    {
                        registry.AddToGroup(connectionId, groupName);
                    }
                }
            }
        }
    }
}
