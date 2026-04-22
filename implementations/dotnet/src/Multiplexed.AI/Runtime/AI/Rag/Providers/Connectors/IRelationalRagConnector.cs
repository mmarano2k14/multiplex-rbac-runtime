using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors
{
    /// <summary>
    /// Defines a concrete relational connector used by the RAG runtime.
    ///
    /// PURPOSE:
    /// - Provides backend-specific relational access.
    /// - Supports multiple relational backends in the same runtime instance.
    /// - Returns raw structured rows for higher-level reader/provider processing.
    ///
    /// DESIGN:
    /// - Each implementation is responsible for one backend or one configured connection.
    /// - Connector selection is key-based and deterministic.
    /// - This contract is infrastructure-only and must not contain domain-specific logic.
    ///
    /// IMPORTANT:
    /// - The same input must produce deterministic rows.
    /// - Implementations must not perform RAG normalization.
    /// </summary>
    public interface IRelationalRagConnector
    {
        /// <summary>
        /// Gets the unique connector key.
        ///
        /// EXAMPLES:
        /// - "sqlserver"
        /// - "postgres"
        /// - "main-sqlserver"
        /// - "reporting-postgres"
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Executes a relational read operation.
        /// </summary>
        /// <param name="entityType">
        /// Logical entity type (for example: "candidate", "job").
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
            string entityType,
            string entityId,
            CancellationToken cancellationToken = default);
    }
}