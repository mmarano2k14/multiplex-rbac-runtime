using Multiplexed.Abstractions.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Mapping;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Resolvers;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores;

namespace Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.SqlServer
{
    /// <summary>
    /// SQL Server candidate RAG datasource.
    ///
    /// PURPOSE:
    /// - Supports both direct and provider execution modes.
    /// - Uses the external store in direct mode.
    /// - Uses the runtime relational connector in provider mode.
    ///
    /// DESIGN:
    /// - Direct mode returns rows mapped from provider-specific entities.
    /// - Provider mode delegates to the runtime connector and returns rows directly.
    ///
    /// IMPORTANT:
    /// - Direct mode and provider mode must follow different execution paths.
    /// </summary>
    public sealed class CandidateSqlServerRagDataSource : AbstractRagRowDataSource
    {
        private readonly ICandidateSqlServerStore _store;
        private readonly IRelationalRagConnectorResolver _connectorResolver;

        public CandidateSqlServerRagDataSource(
            ICandidateSqlServerStore store,
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
                    "RagQueryRequest.Id is required for candidate SQL Server direct mode.");
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
                    "RagQueryRequest.Id is required for candidate SQL Server provider mode.");
            }

            var connector = _connectorResolver.Resolve("sqlserver");

            return connector.ReadAsync(
                entityType: "candidate",
                entityId: request.Id,
                cancellationToken: cancellationToken);
        }
    }
}