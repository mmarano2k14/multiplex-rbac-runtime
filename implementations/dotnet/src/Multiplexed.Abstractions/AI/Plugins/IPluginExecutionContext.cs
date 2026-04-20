// File: IPluginExecutionContext.cs

using System.Collections.Generic;
using Multiplexed.Abstractions.Core.ExecutionContext;

namespace Multiplexed.Abstractions.AI.Plugins
{
    /// <summary>
    /// Represents the execution context passed by the runtime to external plugins.
    ///
    /// IMPORTANT:
    /// - ExecutionContext is the strongly typed runtime execution context used by the AI engine.
    /// - ExecutionContextSnapshot is the persisted RBAC/runtime snapshot captured at execution creation time.
    /// - Inputs are the resolved values prepared by the runtime for the current step.
    ///
    /// DESIGN:
    /// - Keeps plugin execution strongly typed
    /// - Exposes runtime context and persisted RBAC snapshot separately
    /// - Avoids exposing live RBAC internals directly
    /// </summary>
    /// <typeparam name="TExecutionContext">
    /// The strongly typed execution context.
    /// </typeparam>
    public interface IPluginExecutionContext<out TExecutionContext>
    {
        /// <summary>
        /// Gets the strongly typed runtime execution context.
        /// </summary>
        TExecutionContext ExecutionContext { get; }

        /// <summary>
        /// Gets the persisted RBAC/runtime execution context snapshot captured at execution creation time.
        /// </summary>
        ExecutionContextSnapshot? ExecutionContextSnapshot { get; }

        /// <summary>
        /// Gets the inputs resolved by the runtime.
        /// </summary>
        IReadOnlyDictionary<string, object?> Inputs { get; }
    }
}