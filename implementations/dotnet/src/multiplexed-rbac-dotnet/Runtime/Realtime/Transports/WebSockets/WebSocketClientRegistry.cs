using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace MultiplexedRbac.Runtime.Realtime.Transports.WebSockets
{
    /// <summary>
    /// Stores active WebSocket connections and allows routing by
    /// user, group, connection id, or broadcast.
    /// </summary>
    public sealed class WebSocketClientRegistry
    {
        private readonly ConcurrentDictionary<string, WebSocketClientConnection> _connections = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _groups = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _users = new();

        public string Add(WebSocket socket, string? userId = null)
        {
            var connectionId = Guid.NewGuid().ToString("N");

            var connection = new WebSocketClientConnection(
                connectionId,
                socket,
                userId);

            _connections[connectionId] = connection;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var bag = _users.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
                bag[connectionId] = 0;
            }

            return connectionId;
        }

        public void Remove(string connectionId)
        {
            if (!_connections.TryRemove(connectionId, out var connection))
                return;

            if (!string.IsNullOrWhiteSpace(connection.UserId) &&
                _users.TryGetValue(connection.UserId, out var userConnections))
            {
                userConnections.TryRemove(connectionId, out _);

                if (userConnections.IsEmpty)
                {
                    _users.TryRemove(connection.UserId, out _);
                }
            }

            foreach (var group in _groups.Values)
            {
                group.TryRemove(connectionId, out _);
            }
        }

        public void AddToGroup(string connectionId, string groupName)
        {
            var group = _groups.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>());
            group[connectionId] = 0;
        }

        public IReadOnlyCollection<WebSocketClientConnection> GetAll()
        {
            return _connections.Values.ToArray();
        }

        public IReadOnlyCollection<WebSocketClientConnection> GetByUser(string userId)
        {
            if (!_users.TryGetValue(userId, out var ids))
                return Array.Empty<WebSocketClientConnection>();

            return ids.Keys
                .Select(id => _connections.TryGetValue(id, out var c) ? c : null)
                .Where(c => c is not null)
                .Cast<WebSocketClientConnection>()
                .ToArray();
        }

        public IReadOnlyCollection<WebSocketClientConnection> GetByGroup(string groupName)
        {
            if (!_groups.TryGetValue(groupName, out var ids))
                return Array.Empty<WebSocketClientConnection>();

            return ids.Keys
                .Select(id => _connections.TryGetValue(id, out var c) ? c : null)
                .Where(c => c is not null)
                .Cast<WebSocketClientConnection>()
                .ToArray();
        }

        public WebSocketClientConnection? GetByConnectionId(string connectionId)
        {
            return _connections.TryGetValue(connectionId, out var connection)
                ? connection
                : null;
        }
    }
}
