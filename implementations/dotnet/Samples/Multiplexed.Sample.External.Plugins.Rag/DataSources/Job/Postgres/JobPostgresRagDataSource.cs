using Multiplexed.Abstractions.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Mapping;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Resolvers;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores;

namespace Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Postgres
{
    public sealed class JobPostgresRagDataSource : AbstractRagRowDataSource
    {
        private readonly IJobPostgresStore _store;
        private readonly IRelationalRagConnectorResolver _connectorResolver;

        public JobPostgresRagDataSource(
            IJobPostgresStore store,
            IRelationalRagConnectorResolver connectorResolver)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _connectorResolver = connectorResolver ?? throw new ArgumentNullException(nameof(connectorResolver));
        }

        protected override async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteDirectRowsAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                throw new InvalidOperationException("RagQueryRequest.Id is required for job Postgres direct mode.");
            }

            var entities = await _store.ReadByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

            return PropertyBasedRagRowMapper.MapMany(entities);
        }

        protected override Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteProviderRowsAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                throw new InvalidOperationException("RagQueryRequest.Id is required for job Postgres provider mode.");
            }

            var connector = _connectorResolver.Resolve("postgres");

            return connector.ReadAsync(
                entityType: "job",
                entityId: request.Id,
                cancellationToken: cancellationToken);
        }
    }
}