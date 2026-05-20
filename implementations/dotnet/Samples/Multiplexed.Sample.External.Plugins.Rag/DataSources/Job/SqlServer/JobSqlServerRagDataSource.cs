using Multiplexed.Abstractions.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Mapping;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Resolvers;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores;

namespace Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.SqlServer
{
    /// <summary>
    /// SQL Server job RAG datasource.
    ///
    /// PURPOSE:
    /// - Supports both direct and provider execution modes.
    /// - Uses the external store in direct mode.
    /// - Uses the runtime relational connector in provider mode.
    /// </summary>
    public sealed class JobSqlServerRagDataSource : AbstractRagRowDataSource
    {
        private readonly IJobSqlServerStore _store;
        private readonly IRelationalRagConnectorResolver _connectorResolver;

        public JobSqlServerRagDataSource(
            IJobSqlServerStore store,
            IRelationalRagConnectorResolver connectorResolver)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _connectorResolver = connectorResolver ?? throw new ArgumentNullException(nameof(connectorResolver));
        }

        /// <inheritdoc />
        protected override async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteDirectRowsAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                throw new InvalidOperationException(
                    "RagQueryRequest.Id is required for job SQL Server direct mode.");
            }

            var entities = await _store.ReadByIdAsync(request.Id, cancellationToken)
                .ConfigureAwait(false);

            return PropertyBasedRagRowMapper.MapMany(entities);
        }

        /// <inheritdoc />
        protected override Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteProviderRowsAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                throw new InvalidOperationException(
                    "RagQueryRequest.Id is required for job SQL Server provider mode.");
            }

            var connector = _connectorResolver.Resolve("sqlserver");

            return connector.ReadAsync(
                entityType: "job",
                entityId: request.Id,
                cancellationToken: cancellationToken);
        }
    }
}