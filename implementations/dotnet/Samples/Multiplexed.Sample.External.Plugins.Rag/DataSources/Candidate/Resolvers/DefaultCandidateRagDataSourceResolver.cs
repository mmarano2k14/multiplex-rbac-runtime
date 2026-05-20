using Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.Postgres;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.SqlServer;

namespace Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.Resolvers
{
    public sealed class DefaultCandidateRagDataSourceResolver : ICandidateRagDataSourceResolver
    {
        private readonly CandidateSqlServerRagDataSource _sqlServer;
        private readonly CandidatePostgresRagDataSource _postgres;

        public DefaultCandidateRagDataSourceResolver(
            CandidateSqlServerRagDataSource sqlServer,
            CandidatePostgresRagDataSource postgres)
        {
            _sqlServer = sqlServer ?? throw new ArgumentNullException(nameof(sqlServer));
            _postgres = postgres ?? throw new ArgumentNullException(nameof(postgres));
        }

        public Multiplexed.AI.Runtime.AI.Rag.Data.AbstractRagRowDataSource Resolve(string providerKey)
        {
            return providerKey switch
            {
                "sqlserver" => _sqlServer,
                "postgres" => _postgres,
                _ => throw new InvalidOperationException($"Unsupported candidate provider '{providerKey}'.")
            };
        }
    }
}