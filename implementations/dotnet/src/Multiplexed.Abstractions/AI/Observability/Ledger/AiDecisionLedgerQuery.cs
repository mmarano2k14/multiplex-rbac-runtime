using System;

namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Defines query filters used to retrieve decision ledger entries.
    /// </summary>
    public sealed class AiDecisionLedgerQuery
    {
        /// <summary>
        /// Gets the optional execution identifier used to filter ledger entries.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Gets the optional run identifier used to filter ledger entries.
        /// </summary>
        public string? RunId { get; init; }

        /// <summary>
        /// Gets the optional step identifier used to filter ledger entries.
        /// </summary>
        public string? StepId { get; init; }

        /// <summary>
        /// Gets the optional step key used to filter ledger entries.
        /// </summary>
        public string? StepKey { get; init; }

        /// <summary>
        /// Gets the optional category used to filter ledger entries.
        /// </summary>
        public AiDecisionLedgerCategory? Category { get; init; }

        /// <summary>
        /// Gets the optional event type used to filter ledger entries.
        /// </summary>
        public string? EventType { get; init; }

        /// <summary>
        /// Gets the optional outcome used to filter ledger entries.
        /// </summary>
        public AiDecisionLedgerOutcome? Outcome { get; init; }

        /// <summary>
        /// Gets the optional runtime instance identifier used to filter ledger entries.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the optional worker identifier used to filter ledger entries.
        /// </summary>
        public string? WorkerId { get; init; }

        /// <summary>
        /// Gets the optional policy key used to filter ledger entries.
        /// </summary>
        public string? PolicyKey { get; init; }

        /// <summary>
        /// Gets the optional provider used to filter ledger entries.
        /// </summary>
        public string? Provider { get; init; }

        /// <summary>
        /// Gets the optional model used to filter ledger entries.
        /// </summary>
        public string? Model { get; init; }

        /// <summary>
        /// Gets the optional operation used to filter ledger entries.
        /// </summary>
        public string? Operation { get; init; }

        /// <summary>
        /// Gets the optional correlation identifier used to filter ledger entries.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Gets the optional trace identifier used to filter ledger entries.
        /// </summary>
        public string? TraceId { get; init; }

        /// <summary>
        /// Gets the optional inclusive lower sequence bound.
        /// </summary>
        public long? SequenceFrom { get; init; }

        /// <summary>
        /// Gets the optional inclusive upper sequence bound.
        /// </summary>
        public long? SequenceTo { get; init; }

        /// <summary>
        /// Gets the optional inclusive lower timestamp bound.
        /// </summary>
        public DateTimeOffset? TimestampFromUtc { get; init; }

        /// <summary>
        /// Gets the optional inclusive upper timestamp bound.
        /// </summary>
        public DateTimeOffset? TimestampToUtc { get; init; }

        /// <summary>
        /// Gets the maximum number of ledger entries to return.
        /// </summary>
        public int? Limit { get; init; }
    }
}