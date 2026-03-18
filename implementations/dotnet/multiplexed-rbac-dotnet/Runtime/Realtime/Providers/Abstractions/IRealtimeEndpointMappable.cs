namespace MultiplexedRbac.Runtime.Realtime.Providers.Abstractions
{
    /// <summary>
    /// Optional contract implemented by realtime providers that need to expose
    /// HTTP/WebSocket endpoints in the ASP.NET routing pipeline.
    ///
    /// Example:
    /// - SignalR provider maps a hub endpoint
    /// - WebSocket provider maps a websocket endpoint
    /// - Null provider does nothing and does not implement this contract
    /// </summary>
    public interface IRealtimeEndpointMappable
    {
        /// <summary>
        /// Maps provider-specific endpoints into the ASP.NET endpoint pipeline.
        /// </summary>
        IEndpointConventionBuilder? MapEndpoints(IEndpointRouteBuilder endpoints, string path);
    }
}
