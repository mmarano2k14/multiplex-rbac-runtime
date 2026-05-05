using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Default implementation of <see cref="IAiRetentionEvictionService"/>.
    /// </summary>
    /// <remarks>
    /// This service owns the physical eviction safety sequence:
    ///
    /// 1. Persist the step payload.
    /// 2. Mark the archived payload index.
    /// 3. Remove the step from hot execution state.
    ///
    /// A step is never removed from hot state unless payload persistence and archive indexing
    /// both succeed.
    /// </remarks>
    public sealed class DefaultAiRetentionEvictionService : IAiRetentionEvictionService
    {
        private readonly IAiStepPayloadStore _stepPayloadStore;
        private readonly IAiStepPayloadIndexStore _stepPayloadIndexStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetentionEvictionService"/> class.
        /// </summary>
        /// <param name="stepPayloadStore">The durable step payload store.</param>
        /// <param name="stepPayloadIndexStore">The archived step payload index store.</param>
        public DefaultAiRetentionEvictionService(
            IAiStepPayloadStore stepPayloadStore,
            IAiStepPayloadIndexStore stepPayloadIndexStore)
        {
            _stepPayloadStore = stepPayloadStore ?? throw new ArgumentNullException(nameof(stepPayloadStore));
            _stepPayloadIndexStore = stepPayloadIndexStore ?? throw new ArgumentNullException(nameof(stepPayloadIndexStore));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<string>> EvictAsync(
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            string reason = "retention",
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepNames);

            var evictedSteps = new List<string>();

            foreach (var stepName in stepNames)
            {
                if (!state.Steps.TryGetValue(stepName, out var step))
                {
                    continue;
                }

                var payload = await _stepPayloadStore.SaveStepAsync(
                        state.ExecutionId,
                        stepName,
                        step,
                        cancellationToken)
                    .ConfigureAwait(false);

                await _stepPayloadIndexStore.MarkArchivedAsync(
                        new AiArchivedStepPayloadIndex
                        {
                            ExecutionId = state.ExecutionId,
                            StepName = stepName,
                            Status = step.Status,
                            Payload = payload,
                            ArchivedAtUtc = DateTime.UtcNow,
                            Reason = string.IsNullOrWhiteSpace(reason)
                                ? "retention"
                                : reason
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                if (state.Steps.Remove(stepName))
                {
                    evictedSteps.Add(stepName);
                }
            }

            return evictedSteps;
        }
    }
}