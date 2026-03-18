namespace MultiplexedRbac.Runtime.Realtime.Providers.Abstractions
{
    /// <summary>
    /// Represents the active realtime provider host used by the runtime.
    ///
    /// The host exposes:
    /// - the active provider instance
    /// - endpoint mapping behavior, if the provider supports it
    ///
    /// This abstraction allows the runtime to remain provider-agnostic.
    /// </summary>
    public interface IRealtimeProviderHost
    {
        /// <summary>
        /// Gets the active realtime provider instance.
        /// </summary>
        IRealtimeProvider Provider { get; }

        /// <summary>
        /// Maps provider-specific endpoints if supported by the active provider.
        /// Otherwise, this method performs no action.
        /// </summary>
        IEndpointConventionBuilder? MapEndpoints(IEndpointRouteBuilder endpoints, string path);
    }
}
