using MultiplexedRbac.Core.Authorization.Attributes;
using MultiplexedRbac.Core.ExecutionContext;

namespace MultiplexedRbac.Runtime
{
    public sealed class NamespaceGuardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IExecutionContextAccessor _accessor;

        public NamespaceGuardMiddleware(
            RequestDelegate next,
            IExecutionContextAccessor accessor)
        {
            _next = next;
            _accessor = accessor;
        }

        public async Task InvokeAsync(HttpContext http)
        {
            var endpoint = http.GetEndpoint();
            var ns = endpoint?.Metadata.GetMetadata<NamespaceAttribute>();

            if (ns is null)
            {
                await _next(http);
                return;
            }

            var ctx = _accessor.Current;

            if (ctx is null ||
                !string.Equals(ctx.CurrentNamespace, ns.Value, StringComparison.OrdinalIgnoreCase))
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await _next(http);
        }
    }
}
