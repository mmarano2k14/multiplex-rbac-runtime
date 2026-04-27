using Multiplexed.Abstractions.AI.Execution.Payloads.Models;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Redis
{
    /// <summary>
    /// Defines a Redis cache for archived step payload index entries.
    /// </summary>
    public interface IAiStepPayloadIndexCache
    {
        Task SetAsync(
            AiArchivedStepPayloadIndex entry,
            CancellationToken cancellationToken = default);

        Task SetManyAsync(
            IReadOnlyCollection<AiArchivedStepPayloadIndex> entries,
            CancellationToken cancellationToken = default);

        Task<AiArchivedStepPayloadIndex?> GetAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
            string executionId,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default);
    }
}