using System.Collections.Generic;
using Multiplexed.Abstractions.AI.Observability.Tracing;

namespace Multiplexed.AI.Runtime.Observability.Tracing
{
    /// <summary>
    /// No-op implementation of <see cref="IAiTraceRecorder"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Provides a zero-cost implementation when trace recording is disabled.
    /// - Avoids conditional logic in the runtime (no need for if checks).
    ///
    /// DESIGN:
    /// - Does not store any trace records.
    /// - Returns an empty snapshot.
    ///
    /// IMPORTANT:
    /// - Safe for high-throughput execution.
    /// - Must not allocate or block.
    /// </remarks>
    public sealed class NoOpAiTraceRecorder : IAiTraceRecorder
    {
        /// <summary>
        /// Records a completed trace record.
        /// </summary>
        /// <param name="record">The trace record.</param>
        /// <remarks>
        /// This implementation intentionally ignores all records.
        /// </remarks>
        public void Record(AiTraceRecord record)
        {
            // Intentionally no-op
        }

        /// <summary>
        /// Returns a snapshot of recorded trace records.
        /// </summary>
        /// <returns>An empty read-only list.</returns>
        public IReadOnlyList<AiTraceRecord> Snapshot()
        {
            return System.Array.Empty<AiTraceRecord>();
        }
    }
}