using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using Multiplexed.AI.Runtime.AI.Rag.Mapping;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores;

namespace Multiplexed.Sample.External.Plugins.Rag.Candidate.Postgres
{
    /// <summary>
    /// PostgreSQL relational RAG query for candidate rows.
    /// </summary>
    public sealed class CandidatePostgresRelationalRagQuery : IRelationalRagQuery
    {
        private readonly ICandidatePostgresStore _store;

        public CandidatePostgresRelationalRagQuery(
            ICandidatePostgresStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string ConnectorKey => "postgres";

        public string EntityType => "candidate";

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