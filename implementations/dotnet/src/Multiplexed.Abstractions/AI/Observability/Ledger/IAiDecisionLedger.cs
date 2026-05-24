using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Observability.Ledger
{
    /// <summary>
    /// Provides append-only access to the durable decision ledger.
    /// </summary>
    /// <remarks>
    /// The decision ledger is an audit stream associated with runtime executions.
    /// It should not be used as the source of truth for execution state.
    /// </remarks>
    public interface IAiDecisionLedger
    {
        /// <summary>
        /// Appends a decision ledger entry.
        /// </summary>
        /// <param name="entry">The entry to append.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous append operation.</returns>
        Task AppendAsync(
            AiDecisionLedgerEntry entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all decision ledger entries associated with an execution.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ordered decision ledger entries for the execution.</returns>
        Task<IReadOnlyList<AiDecisionLedgerEntry>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries decision ledger entries.
        /// </summary>
        /// <param name="query">The query filters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ordered decision ledger entries matching the query.</returns>
        Task<IReadOnlyList<AiDecisionLedgerEntry>> QueryAsync(
            AiDecisionLedgerQuery query,
            CancellationToken cancellationToken = default);
    }
}