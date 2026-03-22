using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MultiplexedRbac.Runtime.Realtime.Context
{
    /// <summary>
    /// Transport-agnostic connection context used by realtime infrastructure
    /// to resolve user identity and other connection-scoped metadata.
    ///
    /// This object is intentionally independent from SignalR so the same
    /// abstraction can later be reused by WebSockets, SSE or other transports.
    /// </summary>
    public sealed class RealtimeConnectionContext
    {
        /// <summary>
        /// Underlying HTTP context when available.
        ///
        /// This is useful for:
        /// - query string access
        /// - headers
        /// - cookies
        /// - request metadata
        /// </summary>
        public HttpContext? HttpContext { get; init; }

        /// <summary>
        /// Authenticated principal when available.
        ///
        /// This allows resolvers based on claims or authenticated identities.
        /// </summary>
        public ClaimsPrincipal? User { get; init; }

        /// <summary>
        /// Transport-level connection identifier.
        /// </summary>
        public string? ConnectionId { get; init; }

        /// <summary>
        /// Optional extensibility bag for transport-specific metadata.
        ///
        /// This can later be used by custom transports without changing
        /// the base abstraction.
        /// </summary>
        public IDictionary<string, object?> Items { get; init; } =
            new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}