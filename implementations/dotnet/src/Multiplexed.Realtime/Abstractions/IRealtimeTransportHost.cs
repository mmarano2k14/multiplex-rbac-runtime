using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Multiplexed.Realtime.Abstractions
{
    /// <summary>
    /// Represents the active realtime transport host used by the runtime.
    ///
    /// The host exposes:
    /// - the active transport instance
    /// - endpoint mapping behavior, if the transport supports it
    ///
    /// This abstraction allows the runtime to remain transport-agnostic.
    /// </summary>
    public interface IRealtimeTransportHost
    {
        /// <summary>
        /// Gets the active realtime transport instance.
        /// </summary>
        IRealtimeTransport Transport { get; }

        /// <summary>
        /// Maps transport-specific endpoints if supported by the active transport.
        /// Otherwise, this method performs no action.
        /// </summary>
        IEndpointConventionBuilder? MapEndpoints(IEndpointRouteBuilder endpoints, string path);
    }
}