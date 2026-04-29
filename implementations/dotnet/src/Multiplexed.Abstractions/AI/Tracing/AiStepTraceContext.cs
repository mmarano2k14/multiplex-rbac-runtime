namespace Multiplexed.Abstractions.AI.Tracing
{
    /// <summary>
    /// Trace context for a single AI pipeline step.
    /// </summary>
    public sealed class AiStepTraceContext
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string? ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the step identifier.
        /// </summary>
        public string? StepId { get; set; }

        /// <summary>
        /// Gets or sets the step type, for example ai.prompt, rag.vector, or decision.score.
        /// </summary>
        public string? StepType { get; set; }

        /// <summary>
        /// Gets or sets the current step status.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets the retry count for the step.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the recovery count for the step.
        /// </summary>
        public int RecoveryCount { get; set; }

        /// <summary>
        /// Gets or sets the worker identifier that owns the step claim.
        /// </summary>
        public string? WorkerId { get; set; }

        /// <summary>
        /// Gets or sets the claim token associated with the step.
        /// </summary>
        public string? ClaimToken { get; set; }
    }
}