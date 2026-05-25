using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Retention.Models;

namespace Multiplexed.AI.Runtime.Execution.Retention.Services
{
    /// <summary>
    /// Default distributed-safe implementation of <see cref="IAiAtomicRetentionEvictionService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service is intended for active distributed executions where multiple workers may
    /// still be claiming, running, completing, failing, or retrying steps concurrently.
    /// </para>
    ///
    /// <para>
    /// The service does not remove steps from the provided in-memory execution state snapshot.
    /// Instead, it persists the step payload, marks the archived payload index, and then asks
    /// the distributed DAG store to apply an atomic retention patch against the current stored
    /// execution state.
    /// </para>
    ///
    /// <para>
    /// This avoids full-state overwrites and prevents retention from breaking active claims.
    /// </para>
    /// </remarks>
    public sealed class DefaultAiAtomicRetentionEvictionService : IAiAtomicRetentionEvictionService
    {
        private readonly IAiDagExecutionEngineServices _engineServices;
        private readonly IAiStepPayloadStore _stepPayloadStore;
        private readonly IAiStepPayloadIndexStore _stepPayloadIndexStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiAtomicRetentionEvictionService"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services, including the distributed DAG store.
        /// </param>
        /// <param name="stepPayloadStore">
        /// The durable step payload store.
        /// </param>
        /// <param name="stepPayloadIndexStore">
        /// The archived step payload index store.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a required dependency is <see langword="null"/>.
        /// </exception>
        public DefaultAiAtomicRetentionEvictionService(
            IAiDagExecutionEngineServices engineServices,
            IAiStepPayloadStore stepPayloadStore,
            IAiStepPayloadIndexStore stepPayloadIndexStore)
        {
            _engineServices = engineServices ?? throw new ArgumentNullException(nameof(engineServices));
            _stepPayloadStore = stepPayloadStore ?? throw new ArgumentNullException(nameof(stepPayloadStore));
            _stepPayloadIndexStore = stepPayloadIndexStore ?? throw new ArgumentNullException(nameof(stepPayloadIndexStore));
        }

        /// <inheritdoc />
        public async Task<AiRetentionPatchResult> EvictAsync(
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            string reason = "retention",
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepNames);

            if (_engineServices.DagStore is null || stepNames.Count == 0)
            {
                return new AiRetentionPatchResult();
            }

            var candidates = new List<AiRetentionPatchCandidate>();

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

                if (!IsSafeCandidateSnapshot(step))
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

                candidates.Add(
                    new AiRetentionPatchCandidate
                    {
                        StepName = stepName,
                        Action = AiRetentionPatchAction.Evict,
                        ExpectedStatus = step.Status,
                        ExpectedClaimToken = step.ClaimToken,
                        ExpectedExecutionId = state.ExecutionId,
                        ArchivePayloadId = payload.ArtifactId,
                        Reason = reason
                    });
            }

            if (candidates.Count == 0)
            {
                return new AiRetentionPatchResult();
            }

            return await _engineServices.DagStore.TryApplyRetentionPatchAsync(
                    state.ExecutionId,
                    candidates,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Determines whether the local snapshot is safe enough to build an atomic retention candidate.
        /// </summary>
        /// <param name="step">
        /// The step state from the local execution state snapshot.
        /// </param>
        /// <returns>
        /// <c>true</c> when the snapshot represents a terminal, unclaimed step; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method is only a pre-filter. The distributed DAG store must still re-check the
        /// current stored state atomically before applying the patch.
        /// </remarks>
        private static bool IsSafeCandidateSnapshot(
            AiStepState step)
        {
            if (step.Status is not AiStepExecutionStatus.Completed and not AiStepExecutionStatus.Failed)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(step.ClaimToken))
            {
                return false;
            }

            return true;
        }

        
    }
}