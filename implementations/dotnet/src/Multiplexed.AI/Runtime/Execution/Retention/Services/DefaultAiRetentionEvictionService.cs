using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Default implementation of <see cref="IAiRetentionEvictionService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service owns the non-atomic in-memory retention eviction path used by
    /// local or single-engine execution flows.
    /// </para>
    ///
    /// <para>
    /// IMPORTANT:
    /// Active retention must not physically remove DAG step shells while execution may
    /// still require them for dependency evaluation, convergence, replay, diagnostics,
    /// or resolver access.
    /// </para>
    ///
    /// <para>
    /// Therefore this service performs logical hot-state eviction:
    /// </para>
    ///
    /// <list type="number">
    /// <item><description>Persist the full step payload externally.</description></item>
    /// <item><description>Mark the archived payload index.</description></item>
    /// <item><description>Keep the DAG step shell in state.</description></item>
    /// <item><description>Clear heavy inline payload/result data.</description></item>
    /// <item><description>Mark the step as evicted from hot state.</description></item>
    /// </list>
    ///
    /// <para>
    /// Physical deletion of step shells should be reserved for terminal cleanup or a
    /// dedicated post-finalization pruning phase.
    /// </para>
    /// </remarks>
    public sealed class DefaultAiRetentionEvictionService : IAiRetentionEvictionService
    {
        private readonly IAiStepPayloadStore _stepPayloadStore;
        private readonly IAiStepPayloadIndexStore _stepPayloadIndexStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetentionEvictionService"/> class.
        /// </summary>
        /// <param name="stepPayloadStore">
        /// The durable step payload store.
        /// </param>
        /// <param name="stepPayloadIndexStore">
        /// The archived step payload index store.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a required dependency is <see langword="null"/>.
        /// </exception>
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
                if (string.IsNullOrWhiteSpace(stepName))
                {
                    continue;
                }

                if (!state.Steps.TryGetValue(stepName, out var step))
                {
                    continue;
                }

                if (!IsSafeEvictionCandidate(step))
                {
                    continue;
                }

                var effectiveReason = string.IsNullOrWhiteSpace(reason)
                    ? "retention-eviction"
                    : reason;

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
                            Reason = effectiveReason
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                ApplyLogicalEviction(
                    step,
                    effectiveReason);

                evictedSteps.Add(stepName);
            }

            return evictedSteps;
        }

        /// <summary>
        /// Determines whether a step can be logically evicted from hot payload state.
        /// </summary>
        /// <param name="step">
        /// The step state to evaluate.
        /// </param>
        /// <returns>
        /// <c>true</c> when the step is terminal and has not already been evicted from hot state;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool IsSafeEvictionCandidate(
            AiStepState step)
        {
            return step.Status is AiStepExecutionStatus.Completed or AiStepExecutionStatus.Failed &&
                   !step.IsEvictedFromHotState;
        }

        /// <summary>
        /// Applies logical hot-state eviction while preserving the DAG step shell.
        /// </summary>
        /// <param name="step">
        /// The step state to mutate.
        /// </param>
        /// <param name="reason">
        /// The retention reason.
        /// </param>
        private static void ApplyLogicalEviction(
            AiStepState step,
            string reason)
        {
            step.Result = null;
            step.InlinePayloadSizeBytes = 0;
            step.IsEvictedFromHotState = true;
        }
    }
}