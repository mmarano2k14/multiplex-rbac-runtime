namespace Multiplexed.Abstractions.AI.Execution.Payloads.Models
{
    /// <summary>
    /// Optional metadata attached to an execution payload.
    ///
    /// PURPOSE:
    /// - Describe what kind of payload is being stored.
    /// - Help durable stores index, inspect, debug, or clean payloads later.
    ///
    /// IMPORTANT:
    /// - Metadata is optional.
    /// - Stores may ignore it.
    /// - Runtime correctness must not depend on metadata being persisted.
    /// </summary>
    public sealed class AiPayloadMetadata
    {
        /// <summary>
        /// Logical payload category.
        ///
        /// Examples:
        /// - step-state
        /// - step-result
        /// - compacted-result
        /// </summary>
        public string? Kind { get; init; }

        /// <summary>
        /// Execution id associated with this payload.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Step name associated with this payload, when applicable.
        /// </summary>
        public string? StepName { get; init; }

        /// <summary>
        /// Payload content type.
        /// </summary>
        public string ContentType { get; init; } = "application/json";

        /// <summary>
        /// Optional reason why this payload was externalized.
        ///
        /// Examples:
        /// - compaction
        /// - eviction
        /// - hybrid-retention
        /// </summary>
        public string? Reason { get; init; }
    }
}