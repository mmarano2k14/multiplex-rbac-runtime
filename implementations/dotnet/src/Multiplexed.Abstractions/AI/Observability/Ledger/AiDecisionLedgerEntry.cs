using System;
using System.Collections.Generic;
using Multiplexed.Abstractions.AI.Observability.Context;

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
        /// Gets the runtime correlation context associated with this ledger entry.
        /// </summary>
        public required AiRuntimeLedgerEventCorrelationContext CorrelationContext { get; init; }

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
        /// Gets the optional reason explaining the runtime decision.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets additional non-sensitive metadata associated with this entry.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }
}