using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models
{
    /// <summary>
    /// Represents a lightweight replay result intended for API responses,
    /// dashboards, diagnostics, and operational tooling.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// - Provides a concise replay outcome without exposing full execution details.
    /// - Suitable for REST APIs, dashboards, and replay listings.
    /// - Allows operators to quickly determine whether a replay audit succeeded.
    ///
    /// Design:
    /// - Derived from <see cref="AiExecutionReplayReport"/>.
    /// - Omits step-level details and validation internals.
    /// - Focuses on replay status, fingerprint validation, and issue counts.
    /// </remarks>
    public sealed class AiExecutionReplaySummaryResponse
    {
        /// <summary>
        /// Gets the execution identifier that was replayed.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the replay mode used for the operation.
        /// </summary>
        public required AiExecutionReplayMode Mode { get; init; }

        /// <summary>
        /// Gets the pipeline name associated with the execution.
        /// </summary>
        public required string PipelineName { get; init; }

        /// <summary>
        /// Gets the execution status at the time the replay report was generated.
        /// </summary>
        public required string Status { get; init; }

        /// <summary>
        /// Gets whether the replay validation succeeded.
        /// </summary>
        public bool ReplayValid { get; init; }

        /// <summary>
        /// Gets whether the reconstructed fingerprint matches
        /// the persisted terminal fingerprint.
        /// </summary>
        public bool FingerprintMatches { get; init; }

        /// <summary>
        /// Gets whether payload references passed validation.
        /// </summary>
        public bool PayloadReferencesValid { get; init; }

        /// <summary>
        /// Gets whether step state validation passed.
        /// </summary>
        public bool StepStateValid { get; init; }

        /// <summary>
        /// Gets whether dependency graph validation passed.
        /// </summary>
        public bool DependencyGraphValid { get; init; }

        /// <summary>
        /// Gets the total number of steps contained in the execution.
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
        /// Gets the total number of validation issues discovered.
        /// </summary>
        public int IssueCount { get; init; }

        /// <summary>
        /// Gets the primary replay failure reason when validation fails.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// Creates a summary response from a replay report.
        /// </summary>
        /// <param name="report">
        /// The replay report.
        /// </param>
        /// <returns>
        /// A lightweight replay summary.
        /// </returns>
        public static AiExecutionReplaySummaryResponse FromReport(
            AiExecutionReplayReport report)
        {
            ArgumentNullException.ThrowIfNull(report);

            return new AiExecutionReplaySummaryResponse
            {
                ExecutionId = report.ExecutionId,
                Mode = report.Mode,
                PipelineName = report.PipelineName ?? string.Empty,
                Status = report.Status ?? string.Empty,

                ReplayValid = report.ReplayValid,
                FingerprintMatches = report.FingerprintMatches,

                PayloadReferencesValid = report.PayloadReferencesValid,
                StepStateValid = report.StepStateValid,
                DependencyGraphValid = report.DependencyGraphValid,

                TotalSteps = report.TotalSteps,
                CompletedSteps = report.CompletedSteps,
                FailedSteps = report.FailedSteps,
                WaitingForRetrySteps = report.WaitingForRetrySteps,

                IssueCount = report.Issues?.Count ?? 0,
                FailureReason = report.FailureReason
            };
        }
    }
}