using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Realtime.Abstractions;

namespace Multiplexed.Realtime.DI
{
    /// <summary>
    /// ASP.NET endpoint routing extensions for the Multiplex realtime runtime.
    /// </summary>
    public static class RealtimeEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps the active realtime provider endpoint, if the active provider
        /// supports endpoint mapping.
        ///
        /// For example:
        /// - SignalR maps a hub endpoint
        /// - Null provider performs no action
        /// </summary>
        public static IEndpointConventionBuilder MapMultiplexRealtime(this IEndpointRouteBuilder endpoints, string path = "/runtime/realtime")
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var providerHost = endpoints.ServiceProvider.GetRequiredService<IRealtimeTransportHost>();

            var builder = providerHost.MapEndpoints(endpoints, path);

            if (builder is null)
                throw new InvalidOperationException("Realtime provider returned null endpoint builder.");

            return builder;
        }
    }
}
