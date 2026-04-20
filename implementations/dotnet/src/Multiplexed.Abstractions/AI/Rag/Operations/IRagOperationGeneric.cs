using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Models;

namespace Multiplexed.Abstractions.AI.Rag.Operations
{
    /// <summary>
    /// Strongly typed RAG operation contract.
    ///
    /// IMPORTANT:
    /// - The runtime validates the execution context type dynamically.
    /// - This interface stays unconstrained because the current runtime execution context
    ///   type is sealed.
    /// </summary>
    /// <typeparam name="TExecutionContext">
    /// Strongly typed execution context expected by the operation.
    /// </typeparam>
    public interface IRagOperation<TExecutionContext> : IRagOperation
    {
        /// <summary>
        /// Executes the operation using a strongly typed plugin execution context.
        /// </summary>
        Task<RagRetrievalBatch> ExecuteAsync(
            IPluginExecutionContext<TExecutionContext> context,
            CancellationToken cancellationToken);
    }
}