using Microsoft.AspNetCore.SignalR;
using MultiplexedRbac.Runtime.Realtime.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Providers.SignalR
{
    /// <summary>
    /// SignalR adapter that bridges the transport-agnostic
    /// realtime user identifier resolver to SignalR's IUserIdProvider contract.
    ///
    /// This keeps the runtime abstraction independent from SignalR while still
    /// allowing Clients.User(userId) routing to work.
    /// </summary>
    public sealed class SignalRUserIdProviderAdapter : IUserIdProvider
    {
        private readonly IRealtimeUserIdentifierResolver _resolver;

        public SignalRUserIdProviderAdapter(IRealtimeUserIdentifierResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Resolves the SignalR user identifier using the transport-agnostic resolver.
        /// </summary>
        public string? GetUserId(HubConnectionContext connection)
        {
            ArgumentNullException.ThrowIfNull(connection);

            var context = new RealtimeConnectionContext
            {
                HttpContext = connection.GetHttpContext(),
                User = connection.User,
                ConnectionId = connection.ConnectionId
            };

            return _resolver.Resolve(context);
        }
    }
}