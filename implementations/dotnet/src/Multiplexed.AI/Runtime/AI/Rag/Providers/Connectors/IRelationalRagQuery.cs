using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors
{
    /// <summary>
    /// Defines a backend-specific relational query executable by a relational connector.
    ///
    /// PURPOSE:
    /// - Allows external or backend-specific query implementations to plug into the runtime.
    /// - Keeps connectors generic and free of domain-specific hardcoding.
    ///
    /// DESIGN:
    /// - Each query is identified by:
    ///   - a connector key (backend / connection identity)
    ///   - an entity type (logical target such as candidate, job)
    /// - Query execution returns structured rows only.
    ///
    /// IMPORTANT:
    /// - Implementations must remain deterministic for the same input.
    /// - This contract does not perform any RAG normalization.
    /// </summary>
    public interface IRelationalRagQuery
    {
        /// <summary>
        /// Gets the connector key supported by this query.
        ///
        /// EXAMPLES:
        /// - "sqlserver"
        /// - "postgres"
        /// - "main-sqlserver"
        /// - "reporting-postgres"
        /// </summary>
        string ConnectorKey { get; }

        /// <summary>
        /// Gets the logical entity type supported by this query.
        /// </summary>
        string EntityType { get; }

        /// <summary>
        /// Executes the relational query for the specified entity identifier.
        /// </summary>
        /// <param name="entityId">
        /// Logical entity identifier.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token.
        /// </param>
        /// <returns>
        /// A deterministic collection of structured rows.
        /// </returns>
        Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteAsync(
            string entityId,
            CancellationToken cancellationToken = default);
    }
}