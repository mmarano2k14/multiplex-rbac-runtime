using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Observability.Context;

namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Provides a no-operation decision ledger recorder.
    /// </summary>
    /// <remarks>
    /// This recorder safely ignores all ledger events and can be used when ledger recording is disabled.
    /// </remarks>
    public sealed class NoOpAiDecisionLedgerRecorder : IAiDecisionLedgerRecorder
    {
        /// <inheritdoc />
        public Task RecordAsync(
            AiRuntimeLedgerEventCorrelationContext context,
            AiDecisionLedgerCategory category,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? reason = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

            return Task.CompletedTask;
        }
    }
}