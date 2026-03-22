using Microsoft.Extensions.Options;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Runtime;
using MultiplexedRbac.Runtime.Realtime.Context;
using MultiplexedRbac.Runtime.Realtime.Dispatching;
using MultiplexedRbac.Runtime.Realtime.Events;
using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Events.Runtime;
using NServiceBus.Pipeline;
using System.Security;
using System.Security.Cryptography;

namespace MultiplexedRbac.Runtime.Messaging.NServiceBus
{
    /// <summary>
    /// Rehydrates the execution context for incoming NServiceBus messages.
    ///
    /// This behavior enforces fail-closed execution:
    /// - the access context header must be present
    /// - the referenced execution context must exist in the context store
    ///
    /// Once resolved, the execution context is attached to the current runtime
    /// scope through the execution context accessor for the duration of the
    /// message handling pipeline.
    /// </summary>
    public sealed class IncomingExecutionContextRehydrateBehavior
        : Behavior<IIncomingLogicalMessageContext>
    {
        private readonly IExecutionContextAccessor _accessor;
        private readonly IContextStore _contextStore;
        private readonly ContextRuntimeOptions _options;
        private readonly IRealtimeEventContext _realtimeEvents;

        public IncomingExecutionContextRehydrateBehavior(
            IExecutionContextAccessor accessor,
            IContextStore contextStore,
            IOptions<ContextRuntimeOptions> options,
            IRealtimeEventContext realtimeEvents)
        {
            _accessor = accessor;
            _contextStore = contextStore;
            _options = options.Value;
            _realtimeEvents = realtimeEvents;
        }

        public override async Task Invoke(
            IIncomingLogicalMessageContext context,
            Func<Task> next)
        {
            // The incoming message must carry the runtime access context header.
            // If it is missing, processing fails closed.
            if (!context.Headers.TryGetValue(
                    _options.AccessContextHeader,
                    out var contextKey) ||
                string.IsNullOrWhiteSpace(contextKey))
            {

                _realtimeEvents.LogWarning(
                    $"Missing {_options.AccessContextHeader} header (fail-closed).",
                    "NServiceBus.ExecutionContext",
                    data: new { Header = _options.AccessContextHeader, MessageId = context.MessageId });

                throw new SecurityException(
                    $"Missing {_options.AccessContextHeader} header (fail-closed).");
            }

            // Resolve the execution context from the distributed context store.
            var executionContext = await _contextStore.GetAsync(contextKey);

            // If the context is missing or expired, processing must stop.
            if (executionContext is null)
            {
                _realtimeEvents.LogWarning(
                    $"ExecutionContext not found or expired for key '{contextKey}'.",
                    "NServiceBus.ExecutionContext",
                    data: new { ContextKey = contextKey, MessageId = context.MessageId });

                throw new SecurityException(
                    $"ExecutionContext not found or expired for key '{contextKey}'.");
            }

            // Attach the execution context to the current runtime scope.
            _accessor.Set(executionContext);

            _realtimeEvents.LogInfo(
                    executionContext.UserId,
                    "EExecutionContext successfully rehydrated for incoming NServiceBus message.",
                    "NServiceBus.ExecutionContext",
                    data: new
                    {
                        ContextKey = contextKey,
                        MessageId = context.MessageId,
                        executionContext.TenantId,
                        executionContext.TenantGroupId,
                        executionContext.CurrentNamespace
                    });

            try
            {
                // Continue the NServiceBus pipeline with the rehydrated context.
                await next();
            }
            finally
            {
                // Always clear the accessor to avoid leaking context across executions.
                _accessor.Clear();

                _realtimeEvents.LogDebug(
                    executionContext.UserId,
                    "ExecutionContext cleared after NServiceBus message processing.",
                    "NServiceBus.ExecutionContext",
                    data: new { MessageId = context.MessageId });
            }
        }
    }
}