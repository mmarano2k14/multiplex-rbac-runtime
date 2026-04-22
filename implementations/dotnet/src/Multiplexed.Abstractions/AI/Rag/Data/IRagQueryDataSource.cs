using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Rag.Data
{
    /// <summary>
    /// Defines a generic external RAG query datasource.
    ///
    /// PURPOSE:
    /// - Provides a shared query-oriented abstraction for external RAG operations.
    /// - Supports multiple query styles through <see cref="RagQueryRequest"/>.
    ///
    /// DESIGN:
    /// - Implementations return structured rows only.
    /// - No RAG normalization into batches or items occurs at this layer.
    ///
    /// IMPORTANT:
    /// - This contract is provider-agnostic and domain-agnostic.
    /// - It is intended for reuse across multiple external plugin projects.
    /// </summary>
    public interface IRagQueryDataSource
    {
        /// <summary>
        /// Executes the specified RAG query request.
        /// </summary>
        /// <param name="request">
        /// The query request.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token.
        /// </param>
        /// <returns>
        /// A deterministic collection of structured rows.
        /// </returns>
        Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken = default);
    }
}