using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Defines the service responsible for safely evicting steps from hot execution state.
    /// </summary>
    /// <remarks>
    /// Implementations must preserve the retention safety order:
    /// save step payload, mark the archived payload index, then remove the step from hot state.
    /// </remarks>
    public interface IAiRetentionEvictionService
    {
        /// <summary>
        /// Safely evicts the selected steps from the provided execution state.
        /// </summary>
        /// <param name="state">The execution state containing the selected steps.</param>
        /// <param name="stepNames">The names of the steps selected for eviction.</param>
        /// <param name="reason">The reason stored in the archived payload index.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The names of the steps that were successfully evicted.</returns>
        Task<IReadOnlyCollection<string>> EvictAsync(
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            string reason = "retention",
            CancellationToken cancellationToken = default);
    }
}