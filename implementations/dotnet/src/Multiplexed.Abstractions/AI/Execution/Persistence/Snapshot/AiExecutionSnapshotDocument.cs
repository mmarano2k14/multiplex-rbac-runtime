using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot
{
    /// <summary>
    /// Represents a durable snapshot of an AI execution.
    ///
    /// This model is storage-agnostic in intent, but is also compatible with
    /// MongoDB document persistence.
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// A serializable representation of the external execution context snapshot.
    /// </typeparam>
    [BsonIgnoreExtraElements]
    public sealed class AiExecutionSnapshotDocument<TContextSnapshot>
    {
        /// <summary>
        /// Gets or sets the MongoDB document identifier.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the unique execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the logical pipeline name.
        /// </summary>
        public string PipelineName { get; set; } = default!;

        /// <summary>
        /// Gets or sets the current execution status.
        /// Stored as string for persistence flexibility.
        /// </summary>
        public string Status { get; set; } = default!;

        /// <summary>
        /// Gets or sets the stable runtime context key.
        /// </summary>
        public string? ContextKey { get; set; }

        /// <summary>
        /// Gets or sets the persisted external context snapshot.
        /// </summary>
        public TContextSnapshot? ContextSnapshot { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the execution was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the snapshot was last updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the execution reached a terminal state.
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the immutable execution record.
        /// </summary>
        public AiExecutionRecord Record { get; set; } = default!;

        /// <summary>
        /// Gets or sets the mutable execution state.
        /// </summary>
        public AiExecutionState State { get; set; } = default!;

        /// <summary>
        /// Gets or sets the persisted step states.
        /// </summary>
        public List<AiStepState> Steps { get; set; } = new();

        /// <summary>
        /// Gets or sets the technical execution events.
        /// </summary>
        public List<AiExecutionEvent> Events { get; set; } = new();
    }
}