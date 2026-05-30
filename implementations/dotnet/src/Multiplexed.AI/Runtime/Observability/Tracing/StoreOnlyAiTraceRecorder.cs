using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Observability.Tracing;
using Multiplexed.Abstractions.AI.Observability.Tracing.Store;

namespace Multiplexed.AI.Runtime.Observability.Tracing
{
    /// <summary>
    /// Trace recorder that writes completed trace records only to a configured
    /// runtime trace store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This recorder is used when in-memory trace recording is disabled but trace
    /// persistence is enabled, for example MongoDB-only tracing.
    /// </para>
    ///
    /// <para>
    /// Store writes are best-effort because tracing is observational and must not
    /// affect runtime execution.
    /// </para>
    /// </remarks>
    public sealed class StoreOnlyAiTraceRecorder : IAiTraceRecorder
    {
        private readonly IAiRuntimeTraceStore _traceStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="StoreOnlyAiTraceRecorder"/> class.
        /// </summary>
        /// <param name="traceStore">The runtime trace store.</param>
        public StoreOnlyAiTraceRecorder(
            IAiRuntimeTraceStore traceStore)
        {
            _traceStore = traceStore
                ?? throw new ArgumentNullException(nameof(traceStore));
        }

        /// <inheritdoc />
        public void Record(
            AiTraceRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await _traceStore.AppendAsync(
                                record,
                                CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort trace persistence.
                        // Tracing must not break runtime execution.
                    }
                });
        }

        /// <inheritdoc />
        public IReadOnlyList<AiTraceRecord> Snapshot()
        {
            return Array.Empty<AiTraceRecord>();
        }
    }
}