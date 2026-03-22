using MultiplexedRbac.Runtime.Realtime.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Transports
{
    /// <summary>
    /// Generic host wrapper for a concrete realtime transport.
    ///
    /// This class centralizes:
    /// - access to the active transport
    /// - optional endpoint mapping if the transport supports it
    ///
    /// This avoids having separate host classes for each transport type.
    /// </summary>
    public sealed class RealtimeTransportHost<TTransport> : IRealtimeTransportHost
        where TTransport : class, IRealtimeTransport
    {
        public RealtimeTransportHost(TTransport transport)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>
        /// Gets the concrete active transport instance.
        /// </summary>
        public TTransport Transport { get; }

        /// <summary>
        /// Gets the active transport as the non-generic abstraction.
        /// </summary>
        IRealtimeTransport IRealtimeTransportHost.Transport => Transport;

        /// <summary>
        /// Maps transport-specific endpoints if the transport implements
        /// <see cref="IRealtimeEndpointMapper"/>.
        ///
        /// If the transport does not expose endpoints, this method is a no-op.
        /// </summary>
        public IEndpointConventionBuilder? MapEndpoints(IEndpointRouteBuilder endpoints, string path)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            if (Transport is IRealtimeEndpointMapper mapper)
            {
                return mapper.MapEndpoints(endpoints, path);
            }

            return null;
        }
    }
}