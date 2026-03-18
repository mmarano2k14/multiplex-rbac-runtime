using Microsoft.AspNetCore.SignalR;

namespace MultiplexedRbac.Runtime.Realtime.Providers.SignalR
{
    /// <summary>
    /// SignalR hub responsible for realtime communication with runtime clients.
    ///
    /// Responsibilities:
    /// - accept realtime connections
    /// - support explicit group subscriptions
    /// - expose connection lifecycle hooks for diagnostics
    /// </summary>
    public sealed class RealtimeHub : Hub
    {
        /// <summary>
        /// Adds the current connection to a named SignalR group.
        /// </summary>
        public async Task JoinGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                throw new HubException("Group name is required.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        /// <summary>
        /// Removes the current connection from a named SignalR group.
        /// </summary>
        public async Task LeaveGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                throw new HubException("Group name is required.");
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        /// <summary>
        /// Hook invoked when a client successfully connects.
        ///
        /// Useful for diagnostics and for verifying resolved user identity.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            // Optional diagnostic point:
            Console.WriteLine($"Realtime connected: {Context.ConnectionId} / {Context.UserIdentifier}");

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Hook invoked when a client disconnects.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}