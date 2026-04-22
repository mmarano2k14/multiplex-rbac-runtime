using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using Multiplexed.AI.Runtime.AI.Rag.Mapping;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores;

namespace Multiplexed.Sample.External.Plugins.Rag.Candidate.SqlServer
{
    /// <summary>
    /// SQL Server relational RAG query for candidate rows.
    ///
    /// PURPOSE:
    /// - Bridges the runtime SQL Server connector to the external candidate store.
    /// - Keeps application entity logic out of the runtime connector.
    ///
    /// DESIGN:
    /// - Uses the provider-specific external store.
    /// - Returns raw structured rows only.
    /// - Uses the runtime property-based row mapper for consistency.
    /// </summary>
    public sealed class CandidateSqlServerRelationalRagQuery : IRelationalRagQuery
    {
        private readonly ICandidateSqlServerStore _store;

        public CandidateSqlServerRelationalRagQuery(
            ICandidateSqlServerStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <inheritdoc />
        public string ConnectorKey => "sqlserver";

        /// <inheritdoc />
        public string EntityType => "candidate";

        /// <inheritdoc />
        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteAsync(
            string entityId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException(
                    "Entity id cannot be null or whitespace.",
                    nameof(entityId));
            }

            var entities = await _store.ReadByIdAsync(entityId, cancellationToken)
                .ConfigureAwait(false);

            return PropertyBasedRagRowMapper.MapMany(entities);
        }
    }
}