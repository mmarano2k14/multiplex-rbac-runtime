using Multiplexed.Abstractions.AI.Execution.Payloads;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the durable state of an AI execution.
    ///
    /// PURPOSE:
    /// - Stores execution-level shared data.
    /// - Stores payload references for compacted execution-level data.
    /// - Stores durable per-step runtime state.
    /// - Stores execution metadata and metadata payload references.
    /// - Tracks state creation and update timestamps.
    ///
    /// DESIGN:
    /// - This type is a persistence model.
    /// - It does not perform payload resolution.
    /// - It does not perform path resolution.
    /// - It does not contain read/write orchestration logic.
    ///
    /// ARCHITECTURE:
    /// - Read behavior belongs to IAiExecutionStateReader.
    /// - Write behavior belongs to IAiExecutionStateWriter.
    /// - Step-scoped value resolution belongs to IAiStepContextHelper.
    /// - Low-level path/payload resolution belongs to IAiContextValueResolver.
    ///
    /// IMPORTANT:
    /// - Step state remains the source of truth for DAG execution.
    /// - This object must remain safe to serialize, persist, restore, replay, and snapshot.
    /// </summary>
    public sealed class AiExecutionState
    {
        /// <summary>
        /// Gets or sets the execution state identifier.
        ///
        /// This normally matches the parent execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the optional pipeline name associated with this execution.
        /// </summary>
        public string? PipelineName { get; set; }

        /// <summary>
        /// Gets or sets the inline execution-level data bag.
        ///
        /// NOTE:
        /// - This is execution-level shared data.
        /// - Large values may be represented by <see cref="DataPayloads"/>.
        /// - Payload-aware readers should prefer <see cref="DataPayloads"/> when present.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets payload-backed execution-level data references.
        ///
        /// RULE:
        /// - When a key exists in both <see cref="Data"/> and <see cref="DataPayloads"/>,
        ///   payload-aware readers should prefer <see cref="DataPayloads"/>.
        /// </summary>
        public Dictionary<string, AiStoredPayload>? DataPayloads { get; set; }

        /// <summary>
        /// Gets or sets durable per-step runtime state.
        ///
        /// IMPORTANT:
        /// - This is the source of truth for DAG step execution.
        /// - Each entry is keyed by logical step name.
        /// </summary>
        public Dictionary<string, AiStepState> Steps { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets inline execution metadata.
        ///
        /// NOTE:
        /// - This bag is intended for technical/runtime metadata.
        /// - It should not contain business-critical payloads.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets payload-backed execution metadata references.
        ///
        /// RULE:
        /// - When a key exists in both <see cref="Metadata"/> and <see cref="MetadataPayloads"/>,
        ///   payload-aware readers should prefer <see cref="MetadataPayloads"/>.
        /// </summary>
        public Dictionary<string, AiStoredPayload>? MetadataPayloads { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when this state was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp when this state was last updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}