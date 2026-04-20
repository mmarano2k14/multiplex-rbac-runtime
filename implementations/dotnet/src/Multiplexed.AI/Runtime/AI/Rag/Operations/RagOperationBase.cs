using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations;

namespace Multiplexed.AI.Runtime.Rag.Operations
{
    /// <summary>
    /// Base class that bridges typed and untyped RAG operation execution.
    ///
    /// PURPOSE:
    /// - Exposes a strongly typed execution path for plugin/domain implementations
    /// - Preserves a non-typed runtime entry point for dynamic dispatch
    /// - Centralizes runtime type validation in one place
    /// </summary>
    /// <typeparam name="TExecutionContext">
    /// The strongly typed execution context expected by the operation.
    /// </typeparam>
    public abstract class RagOperationBase<TExecutionContext> : IRagOperation<TExecutionContext>
    {
        /// <inheritdoc />
        public abstract string Key { get; }

        /// <inheritdoc />
        public Type ExecutionContextType => typeof(TExecutionContext);

        /// <inheritdoc />
        public abstract Task<RagRetrievalBatch> ExecuteAsync(
            IPluginExecutionContext<TExecutionContext> context,
            CancellationToken cancellationToken);

        /// <inheritdoc />
        public async Task<RagRetrievalBatch> ExecuteUntypedAsync(
            object context,
            CancellationToken cancellationToken)
        {
            if (context is not IPluginExecutionContext<TExecutionContext> typedContext)
            {
                throw new InvalidOperationException(
                    $"Operation '{Key}' expects plugin context '{typeof(IPluginExecutionContext<TExecutionContext>).FullName}', " +
                    $"but received '{context?.GetType().FullName ?? "null"}'.");
            }

            return await ExecuteAsync(typedContext, cancellationToken).ConfigureAwait(false);
        }
    }
}