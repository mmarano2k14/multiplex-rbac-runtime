using System.Net.WebSockets;

namespace MultiplexedRbac.Runtime.Realtime.Providers.WebSockets
{
    /// <summary>
    /// Represents one active WebSocket client connection.
    /// </summary>
    public sealed class WebSocketClientConnection
    {
        public WebSocketClientConnection(string connectionId, WebSocket socket, string? userId)
        {
            ConnectionId = connectionId;
            Socket = socket;
            UserId = userId;
        }

        public string ConnectionId { get; }
        public WebSocket Socket { get; }
        public string? UserId { get; }
    }
}
