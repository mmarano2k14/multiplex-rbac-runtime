using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers
{
    /// <summary>
    /// Defines a provider capable of retrieving and normalizing data into
    /// a unified RAG format.
    ///
    /// PURPOSE:
    /// - Acts as the data access layer of the RAG system.
    /// - Retrieves data from a concrete source (SQL, vector DB, runtime state, etc).
    /// - Transforms raw data into <see cref="RagNormalizedItem"/> instances.
    ///
    /// DESIGN:
    /// - Providers are implementation-specific.
    /// - They may internally use strongly typed models.
    /// - They MUST output normalized items for orchestration compatibility.
    ///
    /// IMPORTANT:
    /// - This is the ONLY contract the retrieval layer depends on.
    /// - All providers must respect deterministic output rules.
    /// </summary>
    public interface INormalizingRagProvider
    {
        /// <summary>
        /// Gets the unique provider key.
        /// Must match the key defined in the attribute.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Executes retrieval and returns normalized results.
        /// </summary>
        /// <param name="context">
        /// The execution context containing query, inputs, and metadata.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token.
        /// </param>
        /// <returns>
        /// A normalized retrieval batch.
        /// </returns>
        Task<RagRetrievalBatch> RetrieveNormalizedAsync(
            RagExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}