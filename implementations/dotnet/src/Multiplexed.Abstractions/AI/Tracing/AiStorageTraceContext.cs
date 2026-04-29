namespace Multiplexed.Abstractions.AI.Tracing
{
    /// <summary>
    /// Trace context for AI runtime storage operations.
    /// </summary>
    public sealed class AiStorageTraceContext
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string? ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the step identifier, when the storage operation is step-scoped.
        /// </summary>
        public string? StepId { get; set; }

        /// <summary>
        /// Gets or sets the storage backend, for example Redis, Mongo, PayloadStore, or SnapshotStore.
        /// </summary>
        public string? Backend { get; set; }

        /// <summary>
        /// Gets or sets the storage operation, for example Load, Save, Claim, Complete, Fail, Recover, or Finalize.
        /// </summary>
        public string? Operation { get; set; }

        /// <summary>
        /// Gets or sets whether the storage operation was a cache hit.
        /// </summary>
        public bool? Hit { get; set; }
    }
}