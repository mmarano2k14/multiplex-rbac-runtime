using MultiplexedRbac.Runtime.Realtime.Providers.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Providers
{
    /// <summary>
    /// Generic host wrapper for a concrete realtime provider.
    ///
    /// This class centralizes:
    /// - access to the active provider
    /// - optional endpoint mapping if the provider supports it
    ///
    /// This avoids having separate host classes for each provider type.
    /// </summary>
    public sealed class RealtimeProviderHost<TProvider> : IRealtimeProviderHost
        where TProvider : class, IRealtimeProvider
    {
        public RealtimeProviderHost(TProvider provider)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Gets the concrete active provider instance.
        /// </summary>
        public TProvider Provider { get; }

        /// <summary>
        /// Gets the active provider as the non-generic abstraction.
        /// </summary>
        IRealtimeProvider IRealtimeProviderHost.Provider => Provider;

        /// <summary>
        /// Maps provider-specific endpoints if the provider implements
        /// <see cref="IRealtimeEndpointMappable"/>.
        ///
        /// If the provider does not expose endpoints, this method is a no-op.
        /// </summary>
        public IEndpointConventionBuilder? MapEndpoints(IEndpointRouteBuilder endpoints, string path)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            if (Provider is IRealtimeEndpointMappable mappable)
            {
                return mappable.MapEndpoints(endpoints, path);
            }

            return null;
        }
    }
}
