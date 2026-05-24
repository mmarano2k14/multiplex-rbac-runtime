using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Observability.Context;

namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Records important runtime decisions into a decision ledger using a runtime correlation context.
    /// </summary>
    public interface IAiDecisionLedgerRecorder
    {
        /// <summary>
        /// Records a decision ledger event.
        /// </summary>
        /// <param name="context">The runtime correlation context.</param>
        /// <param name="category">The decision ledger category.</param>
        /// <param name="eventType">The stable event type.</param>
        /// <param name="outcome">The event outcome.</param>
        /// <param name="reason">The optional decision reason.</param>
        /// <param name="metadata">The optional non-sensitive metadata.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous record operation.</returns>
        Task RecordAsync(
            AiRuntimeCorrelationContext context,
            AiDecisionLedgerCategory category,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? reason = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default);
    }
}