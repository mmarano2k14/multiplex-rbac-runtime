namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Represents the result of an atomic retention patch operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This result is returned by the distributed DAG store after it has atomically
    /// evaluated retention patch candidates against the current stored execution state.
    /// </para>
    ///
    /// <para>
    /// Candidates can be compacted, evicted, or skipped. A skipped step is not an error;
    /// it means the step was no longer safe to modify because its stored state changed
    /// after the retention decision was made.
    /// </para>
    /// </remarks>
    public sealed class AiRetentionPatchResult
    {
        /// <summary>
        /// Gets the step names whose payloads were compacted while preserving the step
        /// shell in hot execution state.
        /// </summary>
        public IReadOnlyCollection<string> CompactedSteps { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Gets the step names that were successfully removed from hot execution state.
        /// </summary>
        public IReadOnlyCollection<string> EvictedSteps { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Gets the step names that were skipped because they were no longer safe to patch.
        /// </summary>
        /// <remarks>
        /// Typical skip reasons include a changed status, a non-empty claim token, a worker
        /// owner, a missing step, or any other store-level safety check failure.
        /// </remarks>
        public IReadOnlyCollection<string> SkippedSteps { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Gets a value indicating whether at least one step was compacted.
        /// </summary>
        public bool HasCompactions => CompactedSteps.Count > 0;

        /// <summary>
        /// Gets a value indicating whether at least one step was evicted.
        /// </summary>
        public bool HasEvictions => EvictedSteps.Count > 0;

        /// <summary>
        /// Gets a value indicating whether at least one candidate was applied.
        /// </summary>
        public bool HasChanges => HasCompactions || HasEvictions;
    }
}