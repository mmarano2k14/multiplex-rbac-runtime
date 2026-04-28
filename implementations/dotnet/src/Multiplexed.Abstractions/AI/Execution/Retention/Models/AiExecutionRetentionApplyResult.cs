namespace Multiplexed.Abstractions.AI.Execution.Retention.Models
{
    /// <summary>
    /// Represents the concrete result of applying execution retention.
    ///
    /// PURPOSE:
    /// - Expose what retention actually changed.
    /// - Allow the engine to refresh only affected resolver/index cache entries.
    /// - Avoid full index reloads for large DAG executions.
    ///
    /// IMPORTANT:
    /// - This is an application result, not a policy plan.
    /// - It contains only operations successfully applied.
    /// </summary>
    public sealed class AiExecutionRetentionApplyResult
    {
        /// <summary>
        /// Gets an empty retention application result.
        /// </summary>
        public static AiExecutionRetentionApplyResult Empty { get; } = new();

        /// <summary>
        /// Gets or sets the steps whose result payloads were compacted.
        /// </summary>
        public IReadOnlyList<string> CompactedSteps { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the steps successfully persisted externally and evicted from hot state.
        /// </summary>
        public IReadOnlyList<string> EvictedSteps { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets whether retention actually changed anything.
        /// </summary>
        public bool HasChanges =>
            CompactedSteps.Count > 0 ||
            EvictedSteps.Count > 0;
    }
}