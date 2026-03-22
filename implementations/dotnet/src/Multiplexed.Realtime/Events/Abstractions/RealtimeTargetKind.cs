namespace Multiplexed.Realtime.Events.Abstractions
{
    /// <summary>
    /// Identifies how a realtime event should be routed to connected clients.
    /// </summary>
    public enum RealtimeTargetKind
    {
        /// <summary>
        /// Broadcast to all connected clients.
        /// </summary>
        All = 0,

        /// <summary>
        /// Send only to a specific authenticated user.
        /// </summary>
        User = 1,

        /// <summary>
        /// Send to a named group of connections.
        /// </summary>
        Group = 2,

        /// <summary>
        /// Send to a specific connection id.
        /// </summary>
        Connection = 3
    }
}
