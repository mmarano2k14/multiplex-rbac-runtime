namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports
{
    /// <summary>
    /// Represents replay information for a single execution step.
    /// </summary>
    public sealed class AiExecutionReplayStepReport
    {
        /// <summary>
        /// Gets the step key.
        /// </summary>
        public required string StepKey { get; init; }

        /// <summary>
        /// Gets the step status.
        /// </summary>
        public string? Status { get; init; }

        /// <summary>
        /// Gets whether the step has a persisted result.
        /// </summary>
        public bool HasResult { get; init; }

        /// <summary>
        /// Gets whether the step result is externalized.
        /// </summary>
        public bool IsExternalized { get; init; }

        /// <summary>
        /// Gets whether the step payload reference is valid.
        /// </summary>
        public bool PayloadReferenceValid { get; init; }

        /// <summary>
        /// Gets the retry count for the step.
        /// </summary>
        public int RetryCount { get; init; }

        /// <summary>
        /// Gets the recovery count for the step.
        /// </summary>
        public int RecoveryCount { get; init; }
    }
}