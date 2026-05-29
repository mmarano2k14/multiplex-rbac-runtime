using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Tracing;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Represents the result of replaying or auditing a persisted AI execution.
    /// </summary>
    public sealed class AiExecutionReplayReport
    {
        /// <summary>
        /// Gets the execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the replay mode used for the operation.
        /// </summary>
        public AiExecutionReplayMode Mode { get; init; }

        /// <summary>
        /// Gets the pipeline name when available.
        /// </summary>
        public string? PipelineName { get; init; }

        /// <summary>
        /// Gets the pipeline key when available.
        /// </summary>
        public string? PipelineKey { get; init; }

        /// <summary>
        /// Gets the execution status.
        /// </summary>
        public string? Status { get; init; }

        /// <summary>
        /// Gets whether the execution was found.
        /// </summary>
        public bool ExecutionFound { get; init; }

        /// <summary>
        /// Gets whether an execution snapshot was found.
        /// </summary>
        public bool SnapshotFound { get; init; }

        /// <summary>
        /// Gets whether the original fingerprint was found.
        /// </summary>
        public bool FingerprintFound { get; init; }

        /// <summary>
        /// Gets the total number of steps.
        /// </summary>
        public int TotalSteps { get; init; }

        /// <summary>
        /// Gets the number of completed steps.
        /// </summary>
        public int CompletedSteps { get; init; }

        /// <summary>
        /// Gets the number of failed steps.
        /// </summary>
        public int FailedSteps { get; init; }

        /// <summary>
        /// Gets the number of steps waiting for retry.
        /// </summary>
        public int WaitingForRetrySteps { get; init; }

        /// <summary>
        /// Gets the number of running steps.
        /// </summary>
        public int RunningSteps { get; init; }

        /// <summary>
        /// Gets the total retry count.
        /// </summary>
        public int RetryCount { get; init; }

        /// <summary>
        /// Gets the total recovery count.
        /// </summary>
        public int RecoveryCount { get; init; }

        /// <summary>
        /// Gets the original persisted fingerprint.
        /// </summary>
        public string? OriginalFingerprint { get; init; }

        /// <summary>
        /// Gets the reconstructed fingerprint.
        /// </summary>
        public string? ReconstructedFingerprint { get; init; }

        /// <summary>
        /// Gets whether the reconstructed fingerprint matches the original fingerprint.
        /// </summary>
        public bool FingerprintMatches { get; init; }

        /// <summary>
        /// Gets whether the dependency graph is valid.
        /// </summary>
        public bool DependencyGraphValid { get; init; }

        /// <summary>
        /// Gets whether step states are valid.
        /// </summary>
        public bool StepStateValid { get; init; }

        /// <summary>
        /// Gets whether payload references are valid.
        /// </summary>
        public bool PayloadReferencesValid { get; init; }

        /// <summary>
        /// Gets whether the replay is valid.
        /// </summary>
        public bool ReplayValid { get; init; }

        /// <summary>
        /// Gets the failure reason when replay validation fails.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// Gets validation issues found during replay.
        /// </summary>
        public IReadOnlyList<AiExecutionReplayIssue> Issues { get; init; } =
            Array.Empty<AiExecutionReplayIssue>();

        /// <summary>
        /// Gets step-level replay details.
        /// </summary>
        public IReadOnlyList<AiExecutionReplayStepReport> Steps { get; init; } =
            Array.Empty<AiExecutionReplayStepReport>();

        public IReadOnlyList<AiDecisionLedgerEntry> LedgerEvents { get; init; } =
            Array.Empty<AiDecisionLedgerEntry>();

        /// <summary>
        /// Gets execution trace events when requested.
        /// </summary>
        public IReadOnlyList<AiTraceEvent> TimelineEvents { get; init; } =
            Array.Empty<AiTraceEvent>();
    }
}