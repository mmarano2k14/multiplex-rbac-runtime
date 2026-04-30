using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Tracing
{
    /// <summary>
    /// Records completed AI runtime trace records.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Provides a storage abstraction for trace records.
    /// - Allows different implementations such as in-memory, OpenTelemetry bridge, Mongo, or custom exporters.
    ///
    /// IMPORTANT:
    /// - This abstraction records completed scopes only.
    /// - It must remain lightweight and safe for high-throughput runtime execution.
    /// </remarks>
    public interface IAiTraceRecorder
    {
        /// <summary>
        /// Records a completed trace record.
        /// </summary>
        /// <param name="record">The trace record to store.</param>
        void Record(AiTraceRecord record);

        /// <summary>
        /// Returns a snapshot of currently recorded trace records.
        /// </summary>
        /// <returns>A read-only list of trace records.</returns>
        IReadOnlyList<AiTraceRecord> Snapshot();
    }
}