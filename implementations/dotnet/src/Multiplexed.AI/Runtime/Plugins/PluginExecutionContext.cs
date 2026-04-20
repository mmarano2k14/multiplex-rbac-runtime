using System;
using System.Collections.Generic;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Plugins
{
    /// <summary>
    /// Default implementation of <see cref="IPluginExecutionContext{TExecutionContext}"/>.
    ///
    /// PURPOSE:
    /// - Carries the strongly typed runtime execution context
    /// - Exposes the persisted RBAC execution context snapshot when available
    /// - Exposes the resolved step inputs prepared by the runtime
    /// </summary>
    public sealed class PluginExecutionContext<TExecutionContext> : IPluginExecutionContext<TExecutionContext>
    {
        public PluginExecutionContext(
            TExecutionContext executionContext,
            ExecutionContextSnapshot? executionContextSnapshot,
            IReadOnlyDictionary<string, object?> inputs)
        {
            ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
            ExecutionContextSnapshot = executionContextSnapshot;
            Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        }

        /// <summary>
        /// Gets the strongly typed runtime execution context.
        /// </summary>
        public TExecutionContext ExecutionContext { get; }

        /// <summary>
        /// Gets the persisted RBAC/runtime execution context snapshot captured at execution creation time.
        /// </summary>
        public ExecutionContextSnapshot? ExecutionContextSnapshot { get; }

        /// <summary>
        /// Gets the resolved step inputs.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Inputs { get; }
    }
}