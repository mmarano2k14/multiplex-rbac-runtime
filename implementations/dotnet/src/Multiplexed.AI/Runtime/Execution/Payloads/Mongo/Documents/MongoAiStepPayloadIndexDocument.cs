using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Documents
{
    /// <summary>
    /// MongoDB document for archived step payload index entries.
    ///
    /// PURPOSE:
    /// - Persist an external index for evicted step states.
    /// - Keep hot execution state small while preserving reloadability.
    ///
    /// IMPORTANT:
    /// - This document does not store the full step state.
    /// - It stores only the payload reference returned by <see cref="IAiStepPayloadStore"/>.
    /// </summary>
    public sealed class MongoAiStepPayloadIndexDocument
    {
        /// <summary>
        /// Gets or sets the MongoDB document id.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the execution id.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the step name.
        /// </summary>
        public string StepName { get; set; } = default!;

        /// <summary>
        /// Gets or sets the terminal status captured when the step was archived.
        /// </summary>
        public AiStepExecutionStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the payload reference for the serialized step state.
        /// </summary>
        public AiStoredPayload Payload { get; set; } = default!;

        /// <summary>
        /// Gets or sets when the step was archived.
        /// </summary>
        public DateTime ArchivedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets why the step was archived.
        /// </summary>
        public string Reason { get; set; } = "retention";

        /// <summary>
        /// Creates a deterministic document id.
        /// </summary>
        public static string BuildId(string executionId, string stepName)
        {
            return $"{executionId}:{stepName}";
        }
    }
}