using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Default replay executor for supported replay modes.
    /// </summary>
    public sealed class DefaultAiExecutionReplayExecutor : IAiExecutionReplayExecutor
    {
        private readonly IAiExecutionReplayValidator _validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionReplayExecutor"/> class.
        /// </summary>
        public DefaultAiExecutionReplayExecutor(
            IAiExecutionReplayValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        /// <inheritdoc />
        public async Task<AiExecutionReplayReport> ExecuteAsync(
            AiExecutionReplayRequest request,
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (request.Mode == AiExecutionReplayMode.ReExecuteAll)
            {
                return new AiExecutionReplayReport
                {
                    ExecutionId = record.ExecutionId,
                    Mode = request.Mode,
                    ExecutionFound = true,
                    SnapshotFound = true,
                    ReplayValid = false,
                    FailureReason = "ReExecuteAll replay mode is not supported yet because it may re-run external providers or side effects.",
                    Issues =
                    [
                        new AiExecutionReplayIssue
                        {
                            Code = "replay.mode.reexecute_all.unsupported",
                            Message = "ReExecuteAll is intentionally blocked until side-effect safety and provider replay isolation are implemented."
                        }
                    ]
                };
            }

            var report = await _validator.ValidateAsync(
                    record,
                    state,
                    cancellationToken)
                .ConfigureAwait(false);

            return new AiExecutionReplayReport
            {
                ExecutionId = report.ExecutionId,
                Mode = request.Mode,
                PipelineName = report.PipelineName,
                PipelineKey = report.PipelineKey,
                Status = report.Status,
                ExecutionFound = report.ExecutionFound,
                SnapshotFound = report.SnapshotFound,
                FingerprintFound = report.FingerprintFound,
                TotalSteps = report.TotalSteps,
                CompletedSteps = report.CompletedSteps,
                FailedSteps = report.FailedSteps,
                WaitingForRetrySteps = report.WaitingForRetrySteps,
                RunningSteps = report.RunningSteps,
                RetryCount = report.RetryCount,
                RecoveryCount = report.RecoveryCount,
                OriginalFingerprint = report.OriginalFingerprint,
                ReconstructedFingerprint = report.ReconstructedFingerprint,
                FingerprintMatches = report.FingerprintMatches,
                DependencyGraphValid = report.DependencyGraphValid,
                StepStateValid = report.StepStateValid,
                PayloadReferencesValid = report.PayloadReferencesValid,
                ReplayValid = report.ReplayValid,
                FailureReason = report.FailureReason,
                Issues = report.Issues,
                Steps = report.Steps
            };
        }
    }
}