using Multiplexed.Realtime.Abstractions;

namespace Multiplexed.Realtime.Transports.SignalR
{
    /// <summary>
    /// Configuration options for the SignalR realtime provider.
    /// </summary>
    public sealed class SignalRRealtimeTransportOptions
    {
        /// <summary>
        /// Name of the CORS policy to apply to the realtime endpoint.
        /// </summary>
        public string CorsPolicy { get; set; } = "SignalRCors";

        /// <summary>
        /// Allowed origins for SignalR negotiate / websocket requests.
        /// </summary>
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional resolver type used to determine the logical realtime user identifier.
        ///
        /// This is intentionally stored as a strategy type instead of a boolean flag
        /// so the runtime can later support multiple resolution approaches
        /// (query string, claims, headers, cookies, composite resolvers, etc.).
        /// </summary>
        internal Type? UserIdentifierResolverType { get; private set; }

        /// <summary>
        /// Configures the resolver strategy used to resolve logical realtime user identifiers.
        /// </summary>
        public void UseUserIdentifier<TResolver>()
            where TResolver : class, IRealtimeUserIdentifierResolver
        {
            UserIdentifierResolverType = typeof(TResolver);
        }
    }
}