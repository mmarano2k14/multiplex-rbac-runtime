using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Represents the persisted working state of an AI execution.
    /// 
    /// This object contains mutable execution data exchanged between steps.
    /// It is intentionally separated from AiExecutionRecord in order to keep
    /// orchestration metadata and execution payload concerns isolated.
    /// </summary>
    public sealed class AiExecutionState
    {
        /// <summary>
        /// Unique identifier of the execution state.
        /// This should typically match the parent execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Shared execution data exchanged between steps.
        /// 
        /// This is the primary state bag used by the pipeline.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Execution metadata used for diagnostics, tracing, orchestration,
        /// or transport-related concerns.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// UTC timestamp of creation.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp of last update.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}