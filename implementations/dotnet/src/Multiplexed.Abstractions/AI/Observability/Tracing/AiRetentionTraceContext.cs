namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Trace context for AI execution retention operations.
    /// </summary>
    public sealed class AiRetentionTraceContext
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string? ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the retention action, for example Compact, Evict, Archive, or Rehydrate.
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Gets or sets the retention policy name.
        /// </summary>
        public string? PolicyName { get; set; }

        /// <summary>
        /// Gets or sets the number of steps inspected by the retention operation.
        /// </summary>
        public int InspectedSteps { get; set; }

        /// <summary>
        /// Gets or sets the number of steps affected by the retention operation.
        /// </summary>
        public int AffectedSteps { get; set; }
    }
}