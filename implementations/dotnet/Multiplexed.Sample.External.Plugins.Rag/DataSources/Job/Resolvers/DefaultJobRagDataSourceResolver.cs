using Multiplexed.AI.Runtime.AI.Rag.Data;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Postgres;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Resolvers;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.SqlServer;

namespace Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Resolvers
{
    /// <summary>
    /// Default implementation of <see cref="IJobRagDataSourceResolver"/>.
    ///
    /// PURPOSE:
    /// - Resolves the correct job RAG datasource based on provider key.
    /// - Enables multi-provider support (SQL Server, PostgreSQL, etc.).
    ///
    /// DESIGN:
    /// - Uses explicit mapping between provider keys and datasources.
    /// - Keeps resolution logic simple and deterministic.
    ///
    /// IMPORTANT:
    /// - Provider keys must match those used in pipeline inputs.
    /// </summary>
    public sealed class DefaultJobRagDataSourceResolver : IJobRagDataSourceResolver
    {
        private readonly JobSqlServerRagDataSource _sqlServer;
        private readonly JobPostgresRagDataSource _postgres;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultJobRagDataSourceResolver"/> class.
        /// </summary>
        public DefaultJobRagDataSourceResolver(
            JobSqlServerRagDataSource sqlServer,
            JobPostgresRagDataSource postgres)
        {
            _sqlServer = sqlServer ?? throw new ArgumentNullException(nameof(sqlServer));
            _postgres = postgres ?? throw new ArgumentNullException(nameof(postgres));
        }

        /// <inheritdoc />
        public AbstractRagRowDataSource Resolve(string providerKey)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                throw new ArgumentException(
                    "Provider key cannot be null or whitespace.",
                    nameof(providerKey));
            }

            return providerKey switch
            {
                "sqlserver" => _sqlServer,
                "postgres" => _postgres,
                _ => throw new InvalidOperationException(
                    $"Unsupported job provider '{providerKey}'.")
            };
        }
    }
}