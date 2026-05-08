namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Represents the runtime context used to evaluate and enforce concurrency limits.
    /// </summary>
    public sealed class AiConcurrencyContext
    {
        /// <summary>
        /// Gets the execution identifier associated with the step.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the pipeline key associated with the execution.
        /// </summary>
        public required string PipelineKey { get; init; }

        /// <summary>
        /// Gets the step identifier being evaluated.
        /// </summary>
        public required string StepId { get; init; }

        /// <summary>
        /// Gets the logical step key associated with the step definition.
        /// </summary>
        public required string StepKey { get; init; }

        /// <summary>
        /// Gets the runtime instance identifier requesting the concurrency slot.
        /// </summary>
        public required string RuntimeInstanceId { get; init; }

        // <summary>
        /// Gets the unique distributed concurrency lease identifier.
        ///
        /// This lease identifier is used to provide:
        /// - idempotent distributed slot acquisition
        /// - crash recovery
        /// - automatic lease expiration through Redis TTL
        /// - safe distributed slot release
        ///
        /// The lease identifier must remain stable for the lifetime
        /// of the claimed execution step.
        /// </summary>
        public required string LeaseId { get; init; }
    }
}