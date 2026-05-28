using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.Abstractions.AI.Tracing.Store;

namespace Multiplexed.AI.Runtime.Tracing.Store
{
    /// <summary>
    /// Composite implementation of <see cref="IAiRuntimeTraceStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This store writes trace records to a primary store and then attempts to write
    /// them to a secondary store.
    /// </para>
    ///
    /// <para>
    /// It is primarily intended for MemoryAndMongo mode, where the primary store is
    /// in-memory for live diagnostics and the secondary store is MongoDB for durable
    /// persistence.
    /// </para>
    ///
    /// <para>
    /// Secondary persistence is best-effort. A transient secondary failure must not
    /// prevent the primary trace store from retaining the trace record.
    /// </para>
    /// </remarks>
    public sealed class CompositeAiRuntimeTraceStore : IAiRuntimeTraceStore
    {
        private readonly IAiRuntimeTraceStore _primary;
        private readonly IAiRuntimeTraceStore _secondary;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAiRuntimeTraceStore"/> class.
        /// </summary>
        /// <param name="primary">The primary trace store.</param>
        /// <param name="secondary">The secondary trace store.</param>
        public CompositeAiRuntimeTraceStore(
            IAiRuntimeTraceStore primary,
            IAiRuntimeTraceStore secondary)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
        }

        /// <inheritdoc />
        public async Task AppendAsync(
            AiTraceRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            await _primary.AppendAsync(
                    record,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await _secondary.AppendAsync(
                        record,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Tracing persistence is observational.
                // The primary store has already accepted the record, so a secondary
                // durable-store failure must not break runtime execution or live diagnostics.
            }
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiTraceRecord>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return _primary.GetByExecutionAsync(
                executionId,
                cancellationToken);
        }
    }
}