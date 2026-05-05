using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Defines the service responsible for applying retention compaction.
    /// </summary>
    /// <remarks>
    /// This service performs physical compaction only.
    /// It does not select steps, evaluate policies, evict state, or mutate retention decisions.
    /// </remarks>
    public interface IAiRetentionCompactionService
    {
        /// <summary>
        /// Compacts the selected step payloads in the provided execution state.
        /// </summary>
        /// <param name="state">The execution state containing the selected steps.</param>
        /// <param name="stepNames">The names of the steps selected for compaction.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The names of the steps that were successfully compacted.</returns>
        Task<IReadOnlyCollection<string>> CompactAsync(
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default);
    }
}