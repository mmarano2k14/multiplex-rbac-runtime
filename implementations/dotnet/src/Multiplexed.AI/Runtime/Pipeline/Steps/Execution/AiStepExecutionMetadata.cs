using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Execution
{
    /// <summary>
    /// Stores in-process execution metadata for a single step.
    ///
    /// PURPOSE:
    /// - Tracks local execution attempts within the current worker
    /// - Provides basic diagnostics (timestamps, errors, attempt count)
    ///
    /// IMPORTANT:
    /// - This class is NOT the source of truth for distributed retry
    /// - Distributed retry is handled by AiStepState and the DAG runtime
    /// - AttemptCount is local only and must not be confused with RetryCount
    /// </summary>
    public sealed class AiStepExecutionMetadata
    {
        /// <summary>
        /// Gets or sets the logical step name.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current in-process execution status of the step.
        ///
        /// IMPORTANT:
        /// - This status reflects local execution state only
        /// - The authoritative execution state is managed by the DAG runtime (AiStepState)
        /// </summary>
        public AiStepExecutionStatus Status { get; set; } = AiStepExecutionStatus.Ready;

        /// <summary>
        /// Gets or sets the number of local execution attempts performed
        /// within the current worker execution cycle.
        ///
        /// SEMANTICS:
        /// - This counter is incremented for each in-process execution attempt
        /// - The first execution attempt counts as 1
        /// - This is NOT equivalent to distributed RetryCount
        ///
        /// EXAMPLE:
        /// - AttemptCount = 1 → first execution
        /// - AttemptCount = 2 → first retry within the same process
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the first execution attempt.
        /// </summary>
        public DateTimeOffset? FirstStartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the latest execution attempt.
        /// </summary>
        public DateTimeOffset? LastStartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the step completed successfully.
        /// </summary>
        public DateTimeOffset? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the last error message observed during execution.
        ///
        /// Used for diagnostics only.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Gets or sets the last exception type observed during execution.
        ///
        /// Useful for debugging or retry classification.
        /// </summary>
        public string? LastExceptionType { get; set; }

        /// <summary>
        /// Returns true when the step has completed successfully
        /// within the current process.
        /// </summary>
        public bool IsCompleted => Status == AiStepExecutionStatus.Completed;
    }
}