using Multiplexed.Abstractions.AI.Rag.Data;

namespace Multiplexed.AI.Runtime.AI.Rag.Data
{
    /// <summary>
    /// Base class for row-oriented RAG datasources supporting both direct and provider modes.
    ///
    /// PURPOSE:
    /// - Centralizes execution-mode dispatch.
    /// - Keeps datasource implementations focused on data access strategy.
    ///
    /// DESIGN:
    /// - Direct mode and provider mode return rows, not entities.
    /// - This avoids forcing provider mode to reconstruct provider-specific entities.
    ///
    /// IMPORTANT:
    /// - Implementations must return deterministic rows for the same input.
    /// </summary>
    public abstract class AbstractRagRowDataSource
    {
        /// <summary>
        /// Executes the specified query request and returns structured rows.
        /// </summary>
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request.ExecutionMode switch
            {
                RagQueryExecutionMode.Direct => ExecuteDirectRowsAsync(request, cancellationToken),
                RagQueryExecutionMode.Provider => ExecuteProviderRowsAsync(request, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(request.ExecutionMode),
                    request.ExecutionMode,
                    "Unsupported execution mode.")
            };
        }

        /// <summary>
        /// Executes the request using direct mode.
        /// </summary>
        protected abstract Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteDirectRowsAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Executes the request using provider mode.
        /// </summary>
        protected abstract Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteProviderRowsAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken);
    }
}