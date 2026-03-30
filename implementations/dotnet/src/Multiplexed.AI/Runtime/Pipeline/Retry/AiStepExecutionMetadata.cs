using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Stores execution metadata for a single step.
    ///
    /// This metadata is persisted inside execution state metadata so the runtime
    /// can support retries, replay, and minimal idempotency.
    /// </summary>
    public sealed class AiStepExecutionMetadata
    {
        /// <summary>
        /// Gets or sets the logical step name.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current execution status.
        /// </summary>
        public AiStepExecutionStatus Status { get; set; } = AiStepExecutionStatus.Pending;

        /// <summary>
        /// Gets or sets the total number of attempts performed for the step.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the first execution attempt.
        /// </summary>
        public DateTimeOffset? FirstStartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the last execution attempt.
        /// </summary>
        public DateTimeOffset? LastStartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the step completed successfully.
        /// </summary>
        public DateTimeOffset? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the last error message observed during execution.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Gets or sets the last exception type observed during execution.
        /// </summary>
        public string? LastExceptionType { get; set; }

        /// <summary>
        /// Returns true when the step has already completed successfully.
        /// </summary>
        public bool IsCompleted => Status == AiStepExecutionStatus.Completed;
    }
}