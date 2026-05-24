using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Multiplexed.AI.Observability.Ledger
{
    /// <summary>
    /// Represents the MongoDB sequence counter document used to assign
    /// monotonic decision ledger sequences per execution.
    /// </summary>
    internal sealed class MongoAiDecisionLedgerSequenceDocument
    {
        /// <summary>
        /// Gets or sets the MongoDB document identifier.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the execution identifier associated with this sequence counter.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the current sequence value for the execution ledger stream.
        /// </summary>
        public long CurrentSequence { get; set; }
    }
}