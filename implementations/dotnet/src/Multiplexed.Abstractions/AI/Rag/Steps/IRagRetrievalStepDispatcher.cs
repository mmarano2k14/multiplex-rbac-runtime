using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Runtime;

namespace Multiplexed.Abstractions.AI.Rag.Steps
{
    /// <summary>
    /// Dispatches RAG retrieval execution dynamically based on operation metadata.
    ///
    /// DESIGN:
    /// - Receives the real AI execution context used by the runtime.
    /// - Resolves the target RAG operation dynamically.
    /// - Internally bridges from AiExecutionContext to the strongly typed operation contract.
    /// </summary>
    public interface IRagRetrievalStepDispatcher
    {
        /// <summary>
        /// Executes a dynamically resolved RAG retrieval operation.
        /// </summary>
        /// <param name="executionContext">
        /// The current AI execution context created by the runtime.
        /// </param>
        /// <param name="inputs">
        /// Resolved step inputs.
        /// </param>
        /// <param name="config">
        /// Retrieval step configuration.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token.
        /// </param>
        /// <returns>
        /// A retrieval batch produced by the resolved RAG operation.
        /// </returns>
        Task<RagRetrievalBatch> ExecuteAsync(
            AiExecutionContext executionContext,
            IReadOnlyDictionary<string, object?> inputs,
            RagRetrievalStepConfig config,
            CancellationToken cancellationToken);
    }
}