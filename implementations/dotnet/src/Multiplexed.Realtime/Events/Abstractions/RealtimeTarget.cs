namespace Multiplexed.Realtime.Events.Abstractions
{
    /// <summary>
    /// Describes the realtime routing target for a runtime event.
    ///
    /// The target is dynamic and belongs to the event instance,
    /// while the public event name remains static and belongs to the event type.
    /// </summary>
    public sealed class RealtimeTarget
    {
        private RealtimeTarget(RealtimeTargetKind kind, string? value)
        {
            Kind = kind;
            Value = value;
        }

        /// <summary>
        /// Routing strategy used by the realtime provider.
        /// </summary>
        public RealtimeTargetKind Kind { get; }

        /// <summary>
        /// Optional routing value.
        /// Required for User, Group, and Connection targets.
        /// Unused for All.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Creates a broadcast target.
        /// </summary>
        public static RealtimeTarget All() => new(RealtimeTargetKind.All, null);

        /// <summary>
        /// Creates a user-targeted event.
        /// </summary>
        public static RealtimeTarget User(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User id cannot be null or whitespace.", nameof(userId));

            return new RealtimeTarget(RealtimeTargetKind.User, userId);
        }

        /// <summary>
        /// Creates a group-targeted event.
        /// </summary>
        public static RealtimeTarget Group(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("Group name cannot be null or whitespace.", nameof(groupName));

            return new RealtimeTarget(RealtimeTargetKind.Group, groupName);
        }

        /// <summary>
        /// Creates a connection-targeted event.
        /// </summary>
        public static RealtimeTarget Connection(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new ArgumentException("Connection id cannot be null or whitespace.", nameof(connectionId));

            return new RealtimeTarget(RealtimeTargetKind.Connection, connectionId);
        }
    }
}
