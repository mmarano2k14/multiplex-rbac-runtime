using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Observability.Ledger;

namespace Multiplexed.AI.Observability.Ledger
{
    /// <summary>
    /// Represents the MongoDB document used to persist an AI decision ledger entry.
    /// </summary>
    /// <remarks>
    /// The document stores correlation fields in a flattened form so MongoDB can index
    /// and query them efficiently, while the public ledger entry keeps the correlation
    /// data grouped in <see cref="AiRuntimeLedgerEventCorrelationContext"/>.
    /// </remarks>
    internal sealed class MongoAiDecisionLedgerEntryDocument
    {
        /// <summary>
        /// Gets or sets the MongoDB document identifier.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the execution identifier used as the primary correlation key.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the sequence number within the execution ledger stream.
        /// </summary>
        public long Sequence { get; set; }

        /// <summary>
        /// Gets or sets the high-level ledger category.
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public AiDecisionLedgerCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the stable event type.
        /// </summary>
        public string EventType { get; set; } = default!;

        /// <summary>
        /// Gets or sets the event outcome.
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public AiDecisionLedgerOutcome Outcome { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the event was created.
        /// </summary>
        public DateTimeOffset TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the optional controller run identifier.
        /// </summary>
        public string? RunId { get; set; }

        /// <summary>
        /// Gets or sets the optional DAG step identifier.
        /// </summary>
        public string? StepId { get; set; }

        /// <summary>
        /// Gets or sets the optional logical step key.
        /// </summary>
        public string? StepKey { get; set; }

        /// <summary>
        /// Gets or sets the optional pipeline name.
        /// </summary>
        public string? PipelineName { get; set; }

        /// <summary>
        /// Gets or sets the optional pipeline version.
        /// </summary>
        public string? PipelineVersion { get; set; }

        /// <summary>
        /// Gets or sets the optional runtime instance identifier.
        /// </summary>
        public string? RuntimeInstanceId { get; set; }

        /// <summary>
        /// Gets or sets the optional worker identifier.
        /// </summary>
        public string? WorkerId { get; set; }

        /// <summary>
        /// Gets or sets the optional policy key.
        /// </summary>
        public string? PolicyKey { get; set; }

        /// <summary>
        /// Gets or sets the optional provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the optional model.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the optional operation.
        /// </summary>
        public string? Operation { get; set; }

        /// <summary>
        /// Gets or sets the optional decision reason.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the optional correlation identifier.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the optional trace identifier.
        /// </summary>
        public string? TraceId { get; set; }

        /// <summary>
        /// Gets or sets the optional distributed claim token.
        /// </summary>
        public string? ClaimToken { get; set; }

        /// <summary>
        /// Gets or sets the optional input payload reference.
        /// </summary>
        public string? InputPayloadRef { get; set; }

        /// <summary>
        /// Gets or sets the optional output payload reference.
        /// </summary>
        public string? OutputPayloadRef { get; set; }

        /// <summary>
        /// Gets or sets the optional human input reference.
        /// </summary>
        public string? HumanInputRef { get; set; }

        /// <summary>
        /// Gets or sets the optional prompt reference.
        /// </summary>
        public string? PromptRef { get; set; }

        /// <summary>
        /// Gets or sets additional non-sensitive metadata.
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Creates a MongoDB document from a decision ledger entry.
        /// </summary>
        /// <param name="entry">The decision ledger entry.</param>
        /// <param name="sequence">The assigned durable sequence.</param>
        /// <returns>The MongoDB decision ledger document.</returns>
        public static MongoAiDecisionLedgerEntryDocument FromEntry(
            AiDecisionLedgerEntry entry,
            long sequence)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(entry.CorrelationContext);

            var context = entry.CorrelationContext;

            return new MongoAiDecisionLedgerEntryDocument
            {
                Id = entry.EntryId,
                ExecutionId = context.ExecutionId,
                Sequence = sequence,
                Category = entry.Category,
                EventType = entry.EventType,
                Outcome = entry.Outcome,
                TimestampUtc = entry.TimestampUtc,
                RunId = context.RunId,
                StepId = context.StepId,
                StepKey = context.StepKey,
                PipelineName = context.PipelineName,
                PipelineVersion = context.PipelineVersion,
                RuntimeInstanceId = context.RuntimeInstanceId,
                WorkerId = context.WorkerId,
                PolicyKey = context.PolicyKey,
                Provider = context.Provider,
                Model = context.Model,
                Operation = context.Operation,
                Reason = entry.Reason,
                CorrelationId = context.CorrelationId,
                TraceId = context.TraceId,
                ClaimToken = context.ClaimToken,
                InputPayloadRef = context.InputPayloadRef,
                OutputPayloadRef = context.OutputPayloadRef,
                HumanInputRef = context.HumanInputRef,
                PromptRef = context.PromptRef,
                Metadata = entry.Metadata is null
                    ? null
                    : new Dictionary<string, string>(entry.Metadata, StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Converts this MongoDB document into a decision ledger entry.
        /// </summary>
        /// <returns>The decision ledger entry.</returns>
        public AiDecisionLedgerEntry ToEntry()
        {
            return new AiDecisionLedgerEntry
            {
                EntryId = Id,
                CorrelationContext = new AiRuntimeLedgerEventCorrelationContext
                {
                    ExecutionId = ExecutionId,
                    RunId = RunId,
                    PipelineName = PipelineName,
                    PipelineVersion = PipelineVersion,
                    StepId = StepId,
                    StepKey = StepKey,
                    RuntimeInstanceId = RuntimeInstanceId,
                    WorkerId = WorkerId,
                    ClaimToken = ClaimToken,
                    PolicyKey = PolicyKey,
                    Provider = Provider,
                    Model = Model,
                    Operation = Operation,
                    InputPayloadRef = InputPayloadRef,
                    OutputPayloadRef = OutputPayloadRef,
                    HumanInputRef = HumanInputRef,
                    PromptRef = PromptRef,
                    TraceId = TraceId,
                    CorrelationId = CorrelationId
                },
                Sequence = Sequence,
                Category = Category,
                EventType = EventType,
                Outcome = Outcome,
                TimestampUtc = TimestampUtc,
                Reason = Reason,
                Metadata = Metadata
            };
        }
    }
}