using Multiplexed.Realtime.Abstractions;
using Multiplexed.Realtime.Context;

namespace Multiplexed.Realtime.Resolvers
{
    /// <summary>
    /// Resolves the logical realtime user identifier from a query string parameter.
    ///
    /// This is especially useful for:
    /// - local development
    /// - runtime console scenarios
    /// - controlled demo environments
    ///
    /// This approach should not be considered a security mechanism by itself.
    /// </summary>
    public sealed class QueryStringRealtimeUserIdentifierResolver
        : IRealtimeUserIdentifierResolver
    {
        private readonly string _parameterName;

        /// <summary>
        /// Creates a query-string-based user identifier resolver.
        /// </summary>
        /// <param name="parameterName">
        /// Query string parameter name used to resolve the user identifier.
        /// Defaults to "userId".
        /// </param>
        public QueryStringRealtimeUserIdentifierResolver(string parameterName = "userId")
        {
            _parameterName = string.IsNullOrWhiteSpace(parameterName)
                ? throw new ArgumentException("Parameter name is required.", nameof(parameterName))
                : parameterName;
        }

        /// <summary>
        /// Resolves the user identifier from the configured query string parameter.
        /// </summary>
        public string? Resolve(RealtimeConnectionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var httpContext = context.HttpContext;

            if (httpContext is null)
            {
                return null;
            }

            var value = httpContext.Request.Query[_parameterName].ToString();

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }
    }
}