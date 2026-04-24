using MongoDB.Bson.Serialization.Attributes;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Mongo
{
    /// <summary>
    /// MongoDB document used to persist externalized AI execution payloads.
    ///
    /// PURPOSE:
    /// - Stores large execution payload content outside the execution state.
    /// - Keeps snapshots and ledger records compact.
    /// - Provides replay-safe payload recovery after process restart.
    ///
    /// IMPORTANT:
    /// - This document is part of replay/recovery infrastructure.
    /// - Payload documents should live at least as long as related execution snapshots.
    /// - Redis may cache this data, but MongoDB remains the durable source of truth.
    /// </summary>
    public sealed class MongoAiPayloadDocument
    {
        /// <summary>
        /// Gets or sets the stable payload identifier.
        /// </summary>
        [BsonId]
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the serialized payload content.
        ///
        /// Usually JSON produced by the execution data policy.
        /// </summary>
        public string Content { get; set; } = default!;

        /// <summary>
        /// Gets or sets the payload size in bytes/chars as measured by the policy/store.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the payload content type.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the UTC timestamp when the payload was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp when the payload was last updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}