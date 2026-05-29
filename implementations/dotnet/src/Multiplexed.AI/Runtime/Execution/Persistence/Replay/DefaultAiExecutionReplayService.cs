using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Execution.Persistence.Normalization;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Restores persisted AI execution snapshots and produces replay validation reports.
    /// </summary>
    /// <typeparam name="TContext">
    /// The serialized execution context snapshot type stored with the execution snapshot.
    /// </typeparam>
    public sealed class DefaultAiExecutionReplayService<TContext> : IAiExecutionReplayService
    {
        private readonly IAiExecutionSnapshotStore<TContext> _snapshotStore;
        private readonly IAiExecutionStore _executionStore;
        private readonly IAiExecutionSnapshotService<TContext> _snapshotService;
        private readonly IAiRuntimeLogger _logger;
        private readonly IAiExecutionReplayExecutor _replayExecutor;
        private readonly IAiDagExecutionStore? _dagStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionReplayService{TContext}"/> class.
        /// </summary>
        public DefaultAiExecutionReplayService(
            IAiExecutionSnapshotStore<TContext> snapshotStore,
            IAiExecutionStore executionStore,
            IAiExecutionSnapshotService<TContext> snapshotService,
            IAiRuntimeLogger logger,
            IAiExecutionReplayExecutor replayExecutor,
            IAiDagExecutionStore? dagStore = null)
        {
            _snapshotStore = snapshotStore
                ?? throw new ArgumentNullException(nameof(snapshotStore));

            _executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));

            _snapshotService = snapshotService
                ?? throw new ArgumentNullException(nameof(snapshotService));

            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));

            _replayExecutor = replayExecutor
                ?? throw new ArgumentNullException(nameof(replayExecutor));

            _dagStore = dagStore;
        }

        /// <inheritdoc />
        public async Task<AiExecutionReplayReport> ReplayAsync(
            AiExecutionReplayRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.ExecutionId))
            {
                throw new ArgumentException(
                    "ExecutionId cannot be null or empty.",
                    nameof(request.ExecutionId));
            }

            var executionId = request.ExecutionId;

            var snapshot = await _snapshotStore.GetAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (snapshot is null)
            {
                _logger.Engine.ExecutionReplaySkipped(
                    executionId,
                    "Snapshot not found.");

                return CreateInvalidReport(
                    executionId,
                    request.Mode,
                    executionFound: false,
                    snapshotFound: false,
                    "Snapshot not found.");
            }

            AiExecutionSnapshotRemapper.Remap(
                snapshot);

            var validationError = ValidateSnapshot(
                snapshot);

            if (validationError is not null)
            {
                _logger.Engine.ExecutionReplaySkipped(
                    executionId,
                    validationError);

                return CreateInvalidReport(
                    executionId,
                    request.Mode,
                    executionFound: true,
                    snapshotFound: true,
                    validationError);
            }

            var replayReport = await _replayExecutor.ExecuteAsync(
                    request,
                    snapshot.Record!,
                    snapshot.State!,
                    cancellationToken)
                .ConfigureAwait(false);

            var existingRecord = await GetExistingRecordAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existingRecord is not null &&
                IsCompatibleExistingRecord(existingRecord, snapshot.Record!))
            {
                _logger.Engine.ExecutionReplaySkipped(
                    executionId,
                    "Replay skipped because a compatible runtime execution already exists.");

                return MergeReport(
                    replayReport,
                    request.Mode,
                    existingRecord.PipelineName,
                    existingRecord.Status.ToString(),
                    snapshot.State!);
            }

            if (request.Mode == AiExecutionReplayMode.AuditOnly)
            {
                return MergeReport(
                    replayReport,
                    request.Mode,
                    snapshot.Record!.PipelineName,
                    snapshot.Record.Status.ToString(),
                    snapshot.State!);
            }

            AiExecutionReplayPreparation.Prepare(
                snapshot.Record,
                snapshot.State);

            await RestoreToAuthoritativeStoreAsync(
                    snapshot.Record!,
                    snapshot.State!,
                    cancellationToken)
                .ConfigureAwait(false);

            await _snapshotService.TryPersistAsync(
                    snapshot.Record!,
                    snapshot.State!,
                    snapshot.ContextKey,
                    snapshot.ContextSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.Engine.ExecutionReplayRestored(
                executionId,
                snapshot.Record!.Status,
                snapshot.State?.Steps?.Count ?? 0);

            return MergeReport(
                replayReport,
                request.Mode,
                snapshot.Record.PipelineName,
                snapshot.Record.Status.ToString(),
                snapshot.State!);
        }

        /// <summary>
        /// Gets an existing execution record from the authoritative DAG store when available,
        /// otherwise from the generic execution store.
        /// </summary>
        private async Task<AiExecutionRecord?> GetExistingRecordAsync(
            string executionId,
            CancellationToken cancellationToken)
        {
            if (_dagStore is not null)
            {
                var dagRecord = await _dagStore.GetRecordAsync(
                        executionId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (dagRecord is not null)
                {
                    return dagRecord;
                }
            }

            return await _executionStore.GetRecordAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Restores an execution record and state into the authoritative runtime store.
        /// </summary>
        private async Task RestoreToAuthoritativeStoreAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            if (_dagStore is not null)
            {
                await _dagStore.RestoreAsync(
                        record,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                return;
            }

            await _executionStore.RestoreAsync(
                    record,
                    state,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that the snapshot contains the minimum data required for replay.
        /// </summary>
        private static string? ValidateSnapshot(
            AiExecutionSnapshotDocument<TContext> snapshot)
        {
            if (snapshot.Record is null)
            {
                return "Snapshot record is missing.";
            }

            if (snapshot.State is null)
            {
                return "Snapshot state is missing.";
            }

            if (string.IsNullOrWhiteSpace(snapshot.ExecutionId))
            {
                return "Snapshot execution id is missing.";
            }

            if (!string.Equals(
                    snapshot.ExecutionId,
                    snapshot.Record.ExecutionId,
                    StringComparison.Ordinal))
            {
                return "Snapshot execution id does not match record execution id.";
            }

            if (!string.Equals(
                    snapshot.ExecutionId,
                    snapshot.State.ExecutionId,
                    StringComparison.Ordinal))
            {
                return "Snapshot execution id does not match state execution id.";
            }

            if (!string.Equals(
                    snapshot.Record.ExecutionId,
                    snapshot.State.ExecutionId,
                    StringComparison.Ordinal))
            {
                return "Record execution id does not match state execution id.";
            }

            if (string.IsNullOrWhiteSpace(snapshot.Record.PipelineName))
            {
                return "Snapshot record pipeline name is missing.";
            }

            return null;
        }

        /// <summary>
        /// Determines whether an existing runtime record represents the same replay target.
        /// </summary>
        private static bool IsCompatibleExistingRecord(
            AiExecutionRecord existingRecord,
            AiExecutionRecord snapshotRecord)
        {
            return string.Equals(
                    existingRecord.ExecutionId,
                    snapshotRecord.ExecutionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    existingRecord.PipelineName,
                    snapshotRecord.PipelineName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    existingRecord.ContextKey,
                    snapshotRecord.ContextKey,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates an invalid replay report for load or snapshot validation failures.
        /// </summary>
        private static AiExecutionReplayReport CreateInvalidReport(
            string executionId,
            AiExecutionReplayMode mode,
            bool executionFound,
            bool snapshotFound,
            string failureReason)
        {
            return new AiExecutionReplayReport
            {
                ExecutionId = executionId,
                Mode = mode,
                ExecutionFound = executionFound,
                SnapshotFound = snapshotFound,
                ReplayValid = false,
                FailureReason = failureReason,
                Issues =
                [
                    new AiExecutionReplayIssue
                    {
                        Code = "replay.snapshot.invalid",
                        Message = failureReason
                    }
                ]
            };
        }

        /// <summary>
        /// Applies snapshot replay context to a replay execution report.
        /// </summary>
        private static AiExecutionReplayReport MergeReport(
            AiExecutionReplayReport replayReport,
            AiExecutionReplayMode mode,
            string? pipelineName,
            string? status,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(replayReport);
            ArgumentNullException.ThrowIfNull(state);

            return new AiExecutionReplayReport
            {
                ExecutionId = replayReport.ExecutionId,
                Mode = mode,

                PipelineName = pipelineName,
                PipelineKey = replayReport.PipelineKey,
                Status = status,

                ExecutionFound = true,
                SnapshotFound = true,

                FingerprintFound = replayReport.FingerprintFound,
                OriginalFingerprint = replayReport.OriginalFingerprint,
                ReconstructedFingerprint = replayReport.ReconstructedFingerprint,
                FingerprintMatches = replayReport.FingerprintMatches,

                DependencyGraphValid = replayReport.DependencyGraphValid,
                StepStateValid = replayReport.StepStateValid,
                PayloadReferencesValid = replayReport.PayloadReferencesValid,

                ReplayValid = replayReport.ReplayValid,
                FailureReason = replayReport.FailureReason,

                TotalSteps = replayReport.TotalSteps,
                CompletedSteps = replayReport.CompletedSteps,
                FailedSteps = replayReport.FailedSteps,
                WaitingForRetrySteps = replayReport.WaitingForRetrySteps,
                RunningSteps = replayReport.RunningSteps,

                RetryCount = replayReport.RetryCount,
                RecoveryCount = replayReport.RecoveryCount,

                Issues = replayReport.Issues,
                Steps = replayReport.Steps
            };
        }
    }
}