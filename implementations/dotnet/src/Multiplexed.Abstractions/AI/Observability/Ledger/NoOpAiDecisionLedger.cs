using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Provides a no-operation decision ledger implementation.
    /// </summary>
    /// <remarks>
    /// This implementation is useful when ledger storage is disabled or unavailable.
    /// </remarks>
    public sealed class NoOpAiDecisionLedger : IAiDecisionLedger
    {
        /// <inheritdoc />
        public Task AppendAsync(
            AiDecisionLedgerEntry entry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiDecisionLedgerEntry>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            IReadOnlyList<AiDecisionLedgerEntry> entries = Array.Empty<AiDecisionLedgerEntry>();

            return Task.FromResult(entries);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiDecisionLedgerEntry>> QueryAsync(
            AiDecisionLedgerQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            IReadOnlyList<AiDecisionLedgerEntry> entries = Array.Empty<AiDecisionLedgerEntry>();

            return Task.FromResult(entries);
        }
    }
}