using Multiplexed.Realtime.Context;

namespace Multiplexed.Realtime.Abstractions
{
    /// <summary>
    /// Resolves a logical realtime user identifier from a transport-agnostic
    /// connection context.
    ///
    /// The returned identifier can then be adapted by a specific transport,
    /// such as SignalR, to support user-targeted realtime delivery.
    /// </summary>
    public interface IRealtimeUserIdentifierResolver
    {
        /// <summary>
        /// Resolves the logical realtime user identifier for the current connection.
        /// Returns null when no identifier can be resolved.
        /// </summary>
        string? Resolve(RealtimeConnectionContext context);
    }
}