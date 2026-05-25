using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Defines the kind of atomic retention patch to apply to a hot execution step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Atomic retention patches are used during distributed execution to avoid replacing
    /// the full execution state while other workers may still be claiming, running,
    /// completing, or failing steps.
    /// </para>
    ///
    /// <para>
    /// <see cref="Compact"/> keeps the step in hot state but removes or externalizes
    /// heavy inline payload data.
    /// </para>
    ///
    /// <para>
    /// <see cref="Evict"/> removes the step from hot state only when the store can prove
    /// that the current stored step is still safe to evict.
    /// </para>
    /// </remarks>
    public enum AiRetentionPatchAction
    {
        /// <summary>
        /// Compacts the step payload while preserving the step shell in hot state.
        /// </summary>
        Compact = 0,

        /// <summary>
        /// Evicts the step from hot state after payload persistence and archive indexing.
        /// </summary>
        Evict = 1
    }

    /// <summary>
    /// Represents a single step candidate for distributed-safe atomic retention patching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This model is passed to a distributed DAG store so that the store can atomically
    /// verify whether a step is still safe to compact or evict before modifying hot state.
    /// </para>
    ///
    /// <para>
    /// The candidate contains the expected step status, claim token, and worker id observed
    /// when the retention decision was made. If the current stored value differs, the store
    /// must skip the candidate instead of overwriting or removing the step.
    /// </para>
    ///
    /// <para>
    /// This prevents active distributed retention from breaking claimed steps owned by
    /// another worker.
    /// </para>
    /// </remarks>
    public sealed class AiRetentionPatchCandidate
    {
        /// <summary>
        /// Gets the logical DAG step instance name.
        /// </summary>
        /// <remarks>
        /// This is the step id inside the execution graph, for example
        /// <c>chaos-step-009</c>. It is not the technical step key.
        /// </remarks>
        public required string StepName { get; init; }

        /// <summary>
        /// Gets the retention patch action to apply.
        /// </summary>
        public required AiRetentionPatchAction Action { get; init; }

        /// <summary>
        /// Gets the expected step status at the time the retention decision was made.
        /// </summary>
        /// <remarks>
        /// Distributed stores should only apply the patch when the current stored status
        /// still matches this value, when provided.
        /// </remarks>
        public AiStepExecutionStatus? ExpectedStatus { get; init; }

        /// <summary>
        /// Gets the expected claim token.
        /// </summary>
        /// <remarks>
        /// For active retention this should normally be <see langword="null"/> or empty.
        /// If the current stored claim token differs, the store must skip this candidate.
        /// </remarks>
        public string? ExpectedClaimToken { get; init; }

        /// <summary>
        /// Gets the expected execution identifier for the candidate step.
        /// </summary>
        /// <remarks>
        /// The distributed store can use this value as an additional safety check to ensure that
        /// the patch is applied only to the execution for which the retention decision was built.
        /// </remarks>
        public string? ExpectedExecutionId { get; init; }

        /// <summary>
        /// Gets the archived payload identifier associated with the step.
        /// </summary>
        /// <remarks>
        /// This value is produced after the payload has been persisted outside hot state.
        /// It can be used by the store to keep a compacted payload reference or eviction
        /// audit metadata.
        /// </remarks>
        public string? ArchivePayloadId { get; init; }

        /// <summary>
        /// Gets the retention reason associated with this patch candidate.
        /// </summary>
        public string? Reason { get; init; }
    }
}