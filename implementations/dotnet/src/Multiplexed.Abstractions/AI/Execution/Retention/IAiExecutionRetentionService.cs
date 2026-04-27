using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Retention
{
    /// <summary>
    /// Applies execution state retention to an <see cref="AiExecutionState"/>.
    ///
    /// PURPOSE:
    /// - Control memory usage of execution state.
    /// - Externalize payload safely.
    /// - Provide incremental updates to runtime components.
    ///
    /// IMPORTANT:
    /// - MUST be safe for distributed execution.
    /// - MUST NOT lose step data.
    /// - MUST return only applied operations.
    /// </summary>
    public interface IAiExecutionRetentionService
    {
        /// <summary>
        /// Applies retention to the given execution state.
        /// </summary>
        /// <param name="state">Execution state.</param>
        /// <param name="mode">Retention mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result describing what was actually applied.</returns>
        ValueTask<AiExecutionRetentionApplyResult> ApplyAsync(
            AiExecutionState state,
            AiExecutionRetentionMode mode,
            CancellationToken cancellationToken = default);
    }
}