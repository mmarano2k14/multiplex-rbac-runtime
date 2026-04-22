using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers.Readers
{
    /// <summary>
    /// Defines a higher-level relational reader used by RAG providers.
    ///
    /// PURPOSE:
    /// - Provides a stable abstraction above relational connectors.
    /// - Supports multiple relational backends through connector selection.
    /// - Decouples providers from backend-specific connector implementations.
    ///
    /// DESIGN:
    /// - The caller specifies which connector must be used.
    /// - The reader remains backend-agnostic.
    /// - The reader does not perform normalization into RAG items.
    ///
    /// IMPORTANT:
    /// - Must remain deterministic for the same input.
    /// - Must not contain domain-specific logic.
    /// </summary>
    public interface IRelationalRagRecordReader
    {
        /// <summary>
        /// Reads structured rows for the specified entity using the specified connector.
        /// </summary>
        /// <param name="connectorKey">
        /// The connector key identifying the relational backend to use.
        /// </param>
        /// <param name="entityType">
        /// Logical entity type.
        /// </param>
        /// <param name="entityId">
        /// Logical entity identifier.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token.
        /// </param>
        /// <returns>
        /// A deterministic collection of structured rows.
        /// </returns>
        Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadAsync(
            string connectorKey,
            string entityType,
            string entityId,
            CancellationToken cancellationToken = default);
    }
}