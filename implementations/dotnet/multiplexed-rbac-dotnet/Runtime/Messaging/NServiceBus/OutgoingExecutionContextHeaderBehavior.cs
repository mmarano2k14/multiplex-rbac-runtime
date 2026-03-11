using MultiplexedRbac.Core.ExecutionContext;
using System.Security;
using NServiceBus.Pipeline;
using Microsoft.Extensions.Options;
using MultiplexedRbac.Runtime;

namespace MultiplexedRbac.Runtime.Messaging.NServiceBus
{
    public sealed class OutgoingExecutionContextHeaderBehavior
    : Behavior<IOutgoingLogicalMessageContext>
    {
        private readonly IExecutionContextAccessor _accessor;
        private readonly ContextRuntimeOptions _options;

        public OutgoingExecutionContextHeaderBehavior(
            IExecutionContextAccessor accessor,
            IOptions<ContextRuntimeOptions> options)
        {
            _accessor = accessor;
            _options = options.Value;
        }

        public override Task Invoke(
            IOutgoingLogicalMessageContext context,
            Func<Task> next)
        {
            var executionContext = _accessor.Current;

            if (executionContext is null)
                throw new SecurityException(
                    "Missing ExecutionContext (fail-closed).");

            if (string.IsNullOrWhiteSpace(executionContext.ContextKey))
                throw new SecurityException(
                    "ExecutionContext has no valid ContextKey.");

            context.Headers[_options.AccessContextHeader]
                = executionContext.ContextKey;

            return next();
        }
    }
}
