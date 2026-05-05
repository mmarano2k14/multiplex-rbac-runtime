using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Default implementation of <see cref="IAiRetentionCompactionService"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Apply physical payload compaction for steps selected by retention policies.
    /// - Keep hot execution state smaller by externalizing heavy inline result data.
    ///
    /// DESIGN:
    /// - This service performs mutation.
    /// - Policies only decide which steps should be compacted.
    /// - The retention engine orchestrates when this service is called.
    ///
    /// IMPORTANT:
    /// - This service does not evict steps.
    /// - This service does not remove entries from <see cref="AiExecutionState.Steps"/>.
    /// - After successful compaction, <see cref="AiStepState.InlinePayloadSizeBytes"/> is reset
    ///   because the step no longer contributes inline payload pressure.
    /// </remarks>
    public sealed class DefaultAiRetentionCompactionService : IAiRetentionCompactionService
    {
        private readonly IAiStepResultPayloadCompactor _compactor;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetentionCompactionService"/> class.
        /// </summary>
        /// <param name="compactor">The step result payload compactor.</param>
        public DefaultAiRetentionCompactionService(IAiStepResultPayloadCompactor compactor)
        {
            _compactor = compactor ?? throw new ArgumentNullException(nameof(compactor));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<string>> CompactAsync(
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepNames);

            var compactedSteps = new List<string>();

            foreach (var stepName in stepNames)
            {
                if (!state.Steps.TryGetValue(stepName, out var step))
                {
                    continue;
                }

                if (step.Result is null)
                {
                    continue;
                }

                await _compactor.CompactAsync(
                        step.Result,
                        cancellationToken)
                    .ConfigureAwait(false);

                step.InlinePayloadSizeBytes = null;

                compactedSteps.Add(stepName);
            }

            return compactedSteps;
        }
    }
}