using Multiplexed.Abstractions.AI.Rag.Data;
using Multiplexed.AI.Runtime.AI.Rag.Mapping;

namespace Multiplexed.AI.Runtime.AI.Rag.Data
{
    /// <summary>
    /// Generic connector-mode RAG adapter.
    ///
    /// PURPOSE:
    /// - Supports connector/provider-style execution over an external reader delegate.
    /// - Reuses the same entity-to-row mapping as query mode.
    ///
    /// DESIGN:
    /// - Aligns connector mode with query mode without duplicating mapping logic.
    ///
    /// IMPORTANT:
    /// - This class returns structured rows only.
    /// - It does not build RAG batches or normalized items.
    /// </summary>
    public sealed class GenericRagConnector<TEntity>
    {
        private readonly Func<RagQueryRequest, CancellationToken, Task<IReadOnlyList<TEntity>>> _reader;

        public GenericRagConnector(
            Func<RagQueryRequest, CancellationToken, Task<IReadOnlyList<TEntity>>> reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadAsync(
            RagQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var entities = await _reader(request, cancellationToken).ConfigureAwait(false);

            return PropertyBasedRagRowMapper.MapMany(entities);
        }
    }
}