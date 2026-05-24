using System;
using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Represents one append-only decision ledger entry associated with a runtime execution.
    /// </summary>
    /// <remarks>
    /// The decision ledger is not the source of truth for execution state.
    /// It records why important runtime decisions happened and how the execution evolved.
    /// Payloads and sensitive data should be stored by reference instead of being embedded directly.
    /// </remarks>
    public sealed class AiDecisionLedgerEntry
    {
        /// <summary>
        /// Gets the unique identifier of this ledger entry.
        /// </summary>
        public required string EntryId { get; init; }

        /// <summary>
        /// Gets the execution identifier used as the primary correlation key.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the monotonic sequence number of the entry within the execution ledger stream.
        /// </summary>
        public long Sequence { get; init; }

        /// <summary>
        /// Gets the high-level category of the ledger event.
        /// </summary>
        public required AiDecisionLedgerCategory Category { get; init; }

        /// <summary>
        /// Gets the stable event type string.
        /// </summary>
        public required string EventType { get; init; }

        /// <summary>
        /// Gets the outcome of the ledger event.
        /// </summary>
        public AiDecisionLedgerOutcome Outcome { get; init; } = AiDecisionLedgerOutcome.None;

        /// <summary>
        /// Gets the UTC timestamp when the ledger event was created.
        /// </summary>
        public required DateTimeOffset TimestampUtc { get; init; }

        /// <summary>
        /// Gets the optional controller run identifier associated with this entry.
        /// </summary>
        public string? RunId { get; init; }

        /// <summary>
        /// Gets the optional DAG step identifier associated with this entry.
        /// </summary>
        public string? StepId { get; init; }

        /// <summary>
        /// Gets the optional logical step key associated with this entry.
        /// </summary>
        public string? StepKey { get; init; }

        /// <summary>
        /// Gets the optional pipeline name associated with this entry.
        /// </summary>
        public string? PipelineName { get; init; }

        /// <summary>
        /// Gets the optional pipeline version associated with this entry.
        /// </summary>
        public string? PipelineVersion { get; init; }

        /// <summary>
        /// Gets the optional runtime instance identifier associated with this entry.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the optional worker identifier associated with this entry.
        /// </summary>
        public string? WorkerId { get; init; }

        /// <summary>
        /// Gets the optional policy key associated with this entry.
        /// </summary>
        public string? PolicyKey { get; init; }

        /// <summary>
        /// Gets the optional provider associated with this entry.
        /// </summary>
        public string? Provider { get; init; }

        /// <summary>
        /// Gets the optional model associated with this entry.
        /// </summary>
        public string? Model { get; init; }

        /// <summary>
        /// Gets the optional operation associated with this entry.
        /// </summary>
        public string? Operation { get; init; }

        /// <summary>
        /// Gets the optional reason explaining the runtime decision.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the optional general correlation identifier associated with this entry.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Gets the optional distributed tracing identifier associated with this entry.
        /// </summary>
        public string? TraceId { get; init; }

        /// <summary>
        /// Gets the optional distributed claim token associated with this entry.
        /// </summary>
        public string? ClaimToken { get; init; }

        /// <summary>
        /// Gets the optional input payload reference associated with this entry.
        /// </summary>
        public string? InputPayloadRef { get; init; }

        /// <summary>
        /// Gets the optional output payload reference associated with this entry.
        /// </summary>
        public string? OutputPayloadRef { get; init; }

        /// <summary>
        /// Gets additional non-sensitive metadata associated with this entry.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }
}