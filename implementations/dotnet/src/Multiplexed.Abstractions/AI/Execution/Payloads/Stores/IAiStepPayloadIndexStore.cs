using Multiplexed.Abstractions.AI.Execution.Payloads.Models;


namespace Multiplexed.Abstractions.AI.Execution.Payloads.Stores
{
    /// <summary>
    /// Defines an external index for archived / evicted step states.
    ///
    /// PURPOSE:
    /// - Keep <see cref="AiExecutionState.Steps"/> hot and bounded.
    /// - Preserve knowledge that an evicted step still exists externally.
    /// - Allow readers, selectors, convergence, replay, and diagnostics to locate
    ///   archived step payloads without storing tombstones inside the hot state.
    ///
    /// IMPORTANT:
    /// - This store is an index, not the payload store itself.
    /// - The actual serialized step state is stored by <see cref="IAiStepPayloadStore"/>.
    /// - The index only records where the archived step payload can be found.
    /// </summary>
    public interface IAiStepPayloadIndexStore
    {
        /// <summary>
        /// Marks a step as archived externally.
        /// </summary>
        Task MarkArchivedAsync(
            AiArchivedStepPayloadIndex entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the archived step index entry for a specific execution step.
        /// </summary>
        Task<AiArchivedStepPayloadIndex?> GetAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all archived step index entries for an execution.
        /// </summary>
        Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an archived step index entry.
        /// </summary>
        Task DeleteAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default);

        // <summary>
        /// Gets multiple archived step index entries for specific execution steps.
        /// </summary>
        Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
            string executionId,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default);
    }
}