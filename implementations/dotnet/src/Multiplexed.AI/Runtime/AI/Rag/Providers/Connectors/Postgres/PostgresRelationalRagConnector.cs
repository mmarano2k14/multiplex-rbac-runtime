using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors.Postgres
{
    /// <summary>
    /// Relational connector for PostgreSQL-backed RAG reads.
    ///
    /// PURPOSE:
    /// - Resolves and executes PostgreSQL-specific relational queries.
    /// - Supports runtime relational reads without embedding domain logic in the connector.
    ///
    /// DESIGN:
    /// - Uses registered <see cref="IRelationalRagQuery"/> implementations.
    /// - Filters queries by:
    ///   - connector key
    ///   - entity type
    /// - Returns raw structured rows only.
    ///
    /// IMPORTANT:
    /// - This connector does not perform normalization.
    /// - This connector must not hardcode application entity logic.
    /// </summary>
    public sealed class PostgresRelationalRagConnector : IRelationalRagConnector
    {
        private readonly IReadOnlyDictionary<string, IRelationalRagQuery> _queries;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgresRelationalRagConnector"/> class.
        /// </summary>
        /// <param name="queries">
        /// The available relational queries.
        /// </param>
        public PostgresRelationalRagConnector(
            IEnumerable<IRelationalRagQuery> queries)
        {
            ArgumentNullException.ThrowIfNull(queries);

            _queries = queries
                .Where(x => x is not null && string.Equals(x.ConnectorKey, Key, StringComparison.Ordinal))
                .GroupBy(x => x.EntityType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Single(),
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public string Key => "postgres";

        /// <inheritdoc />
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadAsync(
            string entityType,
            string entityId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ArgumentException("Entity type cannot be null or whitespace.", nameof(entityType));
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException("Entity id cannot be null or whitespace.", nameof(entityId));
            }

            if (!_queries.TryGetValue(entityType, out var query))
            {
                throw new InvalidOperationException(
                    $"No PostgreSQL relational RAG query is registered for entity type '{entityType}'.");
            }

            return query.ExecuteAsync(entityId, cancellationToken);
        }
    }
}