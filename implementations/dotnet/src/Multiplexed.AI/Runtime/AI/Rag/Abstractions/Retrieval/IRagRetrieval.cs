using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval
{
    /// <summary>
    /// Defines a retrieval strategy responsible for orchestrating one or more RAG providers.
    ///
    /// PURPOSE:
    /// - Coordinates data retrieval across multiple providers.
    /// - Applies a retrieval strategy (vector, SQL, runtime, multi-provider, etc).
    /// - Produces a unified <see cref="RagRetrievalBatch"/> containing normalized items.
    ///
    /// DESIGN:
    /// - Retrieval is a strategy layer, not a data access layer.
    /// - It does NOT fetch data directly.
    /// - It delegates data access to <c>INormalizingRagProvider</c> implementations.
    /// - It may merge, filter, rank, or transform results.
    ///
    /// RUNTIME INTEGRATION:
    /// - Retrieval is typically executed inside a DAG step.
    /// - It is designed to be deterministic and replay-safe.
    /// - It must be compatible with distributed execution.
    ///
    /// IMPORTANT:
    /// - Retrieval must produce deterministic output for identical inputs.
    /// - Implementations should avoid non-deterministic behavior (e.g. unordered collections).
    /// - The returned <see cref="RagRetrievalBatch"/> may include diagnostics for observability.
    /// </summary>
    public interface IRagRetrieval
    {
        /// <summary>
        /// Executes the retrieval strategy.
        ///
        /// FLOW:
        /// 1. Read inputs from <paramref name="context"/>
        /// 2. Delegate data fetching to providers
        /// 3. Aggregate and normalize results
        /// 4. Return a deterministic retrieval batch
        ///
        /// IMPORTANT:
        /// - Must be safe for retry, replay, and distributed execution.
        /// - Must respect deterministic ordering guarantees.
        /// </summary>
        /// <param name="context">
        /// The RAG execution context containing query, inputs, and metadata.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A <see cref="RagRetrievalBatch"/> containing normalized items
        /// and optional diagnostics.
        /// </returns>
        Task<RagRetrievalBatch> RetrieveAsync(
            RagExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}