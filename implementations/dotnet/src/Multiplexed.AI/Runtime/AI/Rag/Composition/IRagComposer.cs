using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition
{
    /// <summary>
    /// Defines a composer responsible for building the final context
    /// from retrieved RAG items.
    ///
    /// PURPOSE:
    /// - Transforms raw retrieval output into a structured context.
    /// - Prepares data for prompt execution.
    /// - Applies ordering, grouping, and formatting.
    ///
    /// DESIGN:
    /// - Input is always normalized (RagRetrievalBatch).
    /// - Output is strongly typed (TContext).
    /// - Must respect deterministic rules.
    ///
    /// IMPORTANT:
    /// - Composer defines the shape of the final prompt context.
    /// </summary>
    /// <typeparam name="TContext">
    /// The final context type produced by the composer.
    /// </typeparam>
    public interface IRagComposer<TContext>
    {
        /// <summary>
        /// Builds the final context from retrieved data.
        /// </summary>
        /// <param name="batch">
        /// The retrieval batch.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token.
        /// </param>
        /// <returns>
        /// A composed context.
        /// </returns>
        Task<RagComposedContext<TContext>> ComposeAsync(
            RagRetrievalBatch batch,
            CancellationToken cancellationToken = default);
    }
}