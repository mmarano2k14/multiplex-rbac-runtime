using Multiplexed.Abstractions.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Mapping;

namespace Multiplexed.AI.Runtime.AI.Rag.Data
{
    /// <summary>
    /// Base class for generic RAG query datasources supporting both direct and provider modes.
    ///
    /// PURPOSE:
    /// - Centralizes execution-mode dispatch.
    /// - Centralizes entity-to-row mapping.
    /// - Allows concrete datasources to implement only their data-access strategy.
    /// </summary>
    public abstract class AbstractRagQueryDataSource<TEntity>
    {
        /// <summary>
        /// Executes the request and returns structured rows.
        /// </summary>
        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<TEntity> entities = request.ExecutionMode switch
            {
                RagQueryExecutionMode.Direct => await ExecuteDirectAsync(request, cancellationToken).ConfigureAwait(false),
                RagQueryExecutionMode.Provider => await ExecuteProviderAsync(request, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(request.ExecutionMode), request.ExecutionMode, "Unsupported execution mode.")
            };

            return PropertyBasedRagRowMapper.MapMany(entities);
        }

        /// <summary>
        /// Executes the request using direct mode.
        /// </summary>
        protected abstract Task<IReadOnlyList<TEntity>> ExecuteDirectAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Executes the request using provider mode.
        /// </summary>
        protected abstract Task<IReadOnlyList<TEntity>> ExecuteProviderAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken);
    }
}