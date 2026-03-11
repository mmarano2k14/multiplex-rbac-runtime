using Microsoft.Extensions.Options;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Runtime;
using NServiceBus.Pipeline;
using System.Security;

namespace MultiplexedRbac.Runtime.Messaging.NServiceBus
{
    public sealed class IncomingExecutionContextRehydrateBehavior
    : Behavior<IIncomingLogicalMessageContext>
    {
        private readonly IExecutionContextAccessor _accessor;
        private readonly IContextStore _contextStore;
        private readonly ContextRuntimeOptions _options;

        public IncomingExecutionContextRehydrateBehavior(
            IExecutionContextAccessor accessor,
            IContextStore contextStore,
            IOptions<ContextRuntimeOptions> options)
        {
            _accessor = accessor;
            _contextStore = contextStore;
            _options = options.Value;
        }

        public override async Task Invoke(
            IIncomingLogicalMessageContext context,
            Func<Task> next)
        {
            if (!context.Headers.TryGetValue(
                    _options.AccessContextHeader,
                    out var contextKey) ||
                string.IsNullOrWhiteSpace(contextKey))
            {
                throw new SecurityException(
                    $"Missing {_options.AccessContextHeader} header (fail-closed).");
            }

            var executionContext =
                await _contextStore.GetAsync(contextKey);

            if (executionContext is null)
            {
                throw new SecurityException(
                    $"ExecutionContext not found or expired for key '{contextKey}'.");
            }

            _accessor.Set(executionContext);

            try
            {
                await next();
            }
            finally
            {
                _accessor.Clear();
            }
        }
    }
}
