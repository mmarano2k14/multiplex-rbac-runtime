using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Defines a distributed-safe atomic retention compaction candidate builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service prepares atomic compaction candidates during active distributed
    /// execution without mutating hot execution state directly.
    /// </para>
    ///
    /// <para>
    /// The distributed DAG store remains responsible for applying the patch atomically
    /// against the latest stored step state.
    /// </para>
    /// </remarks>
    public interface IAiAtomicRetentionCompactionService
    {
        /// <summary>
        /// Builds atomic retention patch candidates for step compaction.
        /// </summary>
        /// <param name="state">
        /// The execution state snapshot used to build retention candidates.
        /// </param>
        /// <param name="stepNames">
        /// The step names selected for compaction.
        /// </param>
        /// <param name="reason">
        /// The retention reason stored in archive metadata.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the operation.
        /// </param>
        /// <returns>
        /// The atomic retention patch candidates.
        /// </returns>
        Task<IReadOnlyCollection<AiRetentionPatchCandidate>> BuildCandidatesAsync(
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            string reason = "retention",
            CancellationToken cancellationToken = default);
    }
}