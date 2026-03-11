using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MultiplexedRbac.Core.ExecutionContext;

namespace MultiplexedRbac.Runtime
{
    public sealed class ExecutionContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IContextStore _store;
        private readonly IExecutionContextAccessor _accessor;
        private readonly ContextRuntimeOptions _opt;
        private readonly ILogger<ExecutionContextMiddleware> _logger;

        public ExecutionContextMiddleware(
            RequestDelegate next,
            IContextStore store,
            IExecutionContextAccessor accessor,
            IOptions<ContextRuntimeOptions> options,
            ILogger<ExecutionContextMiddleware> logger)
        {
            _next = next;
            _store = store;
            _accessor = accessor;
            _opt = options.Value;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext http)
        {
            // ------------------------------------------------------------
            // 1. Authentication gate
            // ------------------------------------------------------------
            if (http.User?.Identity?.IsAuthenticated != true)
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // ------------------------------------------------------------
            // 2. Extract Access Context handle
            // ------------------------------------------------------------
            var ctxKey = http.Request.Headers[_opt.AccessContextHeader].ToString();

            if (string.IsNullOrWhiteSpace(ctxKey))
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // ------------------------------------------------------------
            // 3. Prepare header overwrite (rotation-safe)
            // ------------------------------------------------------------

            http.Response.OnStarting(() =>
            {
                if (http.Response.StatusCode < 400)
                {
                    return RotateAndSetHeaderAsync(http, _store, ctxKey, _opt);
                }

                return Task.CompletedTask;
            });


            // ------------------------------------------------------------
            // 4. Acquire In-Flight (Lua atomic)
            // ------------------------------------------------------------
            if (!await _store.TryAcquireInFlightAsync(ctxKey))
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            try
            {
                // ------------------------------------------------------------
                // 5. Resolve ExecutionContext from Redis
                // ------------------------------------------------------------
                var ctx = await _store.GetAsync(ctxKey);

                if (ctx is null)
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                // ------------------------------------------------------------
                // 6. Bind to authenticated principal (anti-replay)
                // ------------------------------------------------------------
                var tokenUserId = http.User.FindFirst("sub")?.Value;

                if (!string.IsNullOrWhiteSpace(tokenUserId) &&
                    !string.Equals(ctx.UserId, tokenUserId, StringComparison.Ordinal))
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                // ------------------------------------------------------------
                // 7. Attach to accessor (official boundary)
                // ------------------------------------------------------------
                ctx.ContextKey = ctxKey;
                _accessor.Set(ctx);

                // ------------------------------------------------------------
                // 8. Execute downstream pipeline
                // ------------------------------------------------------------
                await _next(http);

                // ------------------------------------------------------------
                // 9. Rotation at end-of-request
                // ------------------------------------------------------------
                /*
                if (http.Response.StatusCode < 400)
                {
                    var rotated = await _store.RotateAsync(ctxKey);
                    rotatedKey = rotated.newKey;
                }
                */
            }
            finally
            {
                // ------------------------------------------------------------
                // 10. Always release In-Flight counter
                // ------------------------------------------------------------
                await _store.ReleaseInFlightAsync(ctxKey);
            }
        }

        async Task RotateAndSetHeaderAsync(
        HttpContext http,
        IContextStore store,
        string ctxKey,
        ContextRuntimeOptions options)
        {
            var rotated = await store.RotateAsync(ctxKey);
            var rotatedKey = rotated.newKey;

            if (!string.IsNullOrWhiteSpace(rotatedKey) &&
                !string.Equals(rotatedKey, ctxKey, StringComparison.Ordinal))
            {
                http.Response.Headers[options.AccessContextHeader] = rotatedKey!;
            }
        }

    }
}
