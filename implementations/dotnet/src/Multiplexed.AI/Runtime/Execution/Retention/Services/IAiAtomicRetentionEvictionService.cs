using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Defines a distributed-safe retention eviction service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service is used during active distributed execution, where multiple workers may
    /// claim, run, complete, or fail steps concurrently.
    /// </para>
    ///
    /// <para>
    /// Unlike <see cref="IAiRetentionEvictionService"/>, this service must not directly remove
    /// steps from an in-memory execution state snapshot and then persist the full state.
    /// Instead, it archives step payloads first and then delegates hot-state modification to
    /// the distributed DAG store through an atomic retention patch operation.
    /// </para>
    ///
    /// <para>
    /// This prevents retention from overwriting claim tokens, running states, or step mutations
    /// owned by other workers.
    /// </para>
    /// </remarks>
    public interface IAiAtomicRetentionEvictionService
    {
        /// <summary>
        /// Builds atomic retention patch candidates for step eviction.
        /// </summary>
        /// <param name="state">
        /// The execution state snapshot used to build retention candidates.
        /// </param>
        /// <param name="stepNames">
        /// The step names selected for eviction.
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

        /// <summary>
        /// Safely applies retention to the selected step names using an atomic store-level patch.
        /// </summary>
        /// <param name="state">
        /// The execution state snapshot used to build retention candidates.
        /// </param>
        /// <param name="stepNames">
        /// The names of the steps selected for retention.
        /// </param>
        /// <param name="reason">
        /// The reason stored in the archived payload index and retention patch metadata.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the operation.
        /// </param>
        /// <returns>
        /// The result of the atomic retention patch operation.
        /// </returns>
        Task<AiRetentionPatchResult> EvictAsync(
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            string reason = "retention",
            CancellationToken cancellationToken = default);
    }
}