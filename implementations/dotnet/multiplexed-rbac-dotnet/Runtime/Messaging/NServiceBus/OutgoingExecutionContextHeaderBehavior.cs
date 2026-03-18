using Microsoft.Extensions.Options;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Runtime.Realtime.Dispatching;
using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Events.Runtime;
using NServiceBus.Pipeline;
using System.Security;

namespace MultiplexedRbac.Runtime.Messaging.NServiceBus
{
    /// <summary>
    /// Adds the current execution context key to outgoing NServiceBus messages.
    ///
    /// This behavior enforces fail-closed propagation:
    /// - an execution context must be present in the current runtime scope
    /// - the execution context must contain a valid context key
    ///
    /// The context key is then attached to the outgoing message headers so that
    /// downstream consumers can rehydrate the execution context.
    /// </summary>
    public sealed class OutgoingExecutionContextHeaderBehavior
        : Behavior<IOutgoingLogicalMessageContext>
    {
        private readonly IExecutionContextAccessor _accessor;
        private readonly ContextRuntimeOptions _options;
        private readonly IRuntimeEventDispatcher _runtimeEventDispatcher;

        public OutgoingExecutionContextHeaderBehavior(
            IExecutionContextAccessor accessor,
            IOptions<ContextRuntimeOptions> options,
            IRuntimeEventDispatcher runtimeEventDispatcher)
        {
            _accessor = accessor;
            _options = options.Value;
            _runtimeEventDispatcher = runtimeEventDispatcher;
        }

        public override async Task Invoke(
            IOutgoingLogicalMessageContext context,
            Func<Task> next)
        {
            // Retrieve the current execution context from the runtime accessor.
            var executionContext = _accessor.Current;

            // Outgoing messages must always carry an execution context.
            // If none is available, the pipeline fails closed.
            if (executionContext is null)
            {
                SafePublishRuntimeLog(
                    level: "Warning",
                    message: "Missing ExecutionContext for outgoing NServiceBus message (fail-closed).",
                    category: "NServiceBus.ExecutionContext",
                    userId: null,
                    data: new
                    {
                        MessageId = context.MessageId
                    });

                throw new SecurityException(
                    "Missing ExecutionContext (fail-closed).");
            }

            // The execution context must contain a valid context key.
            // This key is required by downstream consumers to rehydrate context.
            if (string.IsNullOrWhiteSpace(executionContext.ContextKey))
            {
                SafePublishRuntimeLog(
                    level: "Warning",
                    message: "ExecutionContext has no valid ContextKey for outgoing NServiceBus message.",
                    category: "NServiceBus.ExecutionContext",
                    userId: executionContext.UserId,
                    data: new
                    {
                        MessageId = context.MessageId,
                        executionContext.TenantId,
                        executionContext.TenantGroupId,
                        executionContext.CurrentNamespace
                    });

                throw new SecurityException(
                    "ExecutionContext has no valid ContextKey.");
            }

            // Propagate the access context key through the configured header.
            context.Headers[_options.AccessContextHeader] = executionContext.ContextKey;

            SafePublishRuntimeLog(
                level: "Information",
                message: "ExecutionContext header added to outgoing NServiceBus message.",
                category: "NServiceBus.ExecutionContext",
                userId: executionContext.UserId,
                data: new
                {
                    MessageId = context.MessageId,
                    Header = _options.AccessContextHeader,
                    ContextKey = executionContext.ContextKey,
                    executionContext.TenantId,
                    executionContext.TenantGroupId,
                    executionContext.CurrentNamespace
                });

            await next();
        }

        /// <summary>
        /// Publishes a structured runtime log event without ever breaking
        /// the main NServiceBus execution flow.
        /// </summary>
        private void SafePublishRuntimeLog(
            string level,
            string message,
            string category,
            string? userId,
            object? data)
        {
            try
            {
                _runtimeEventDispatcher.Dispatch(
                    new RuntimeLogEvent
                    {
                        Level = level,
                        Message = message,
                        Category = category,
                        UserId = userId,
                        Data = data,

                        // Route low-level technical logs to the runtime console group.
                        RealtimeTarget = userId != null ? RealtimeTarget.User(userId) : RealtimeTarget.Group("runtime-console")
                    });
            }
            catch
            {
                // Observability must never break the transport pipeline.
            }
        }
    }
}