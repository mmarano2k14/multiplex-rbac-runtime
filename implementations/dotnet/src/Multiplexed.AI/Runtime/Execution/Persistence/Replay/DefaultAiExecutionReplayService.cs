using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Execution.Persistence.Normalization;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Default replay service that restores a persisted execution snapshot
    /// back into the runtime execution store.
    ///
    /// PURPOSE:
    /// - Reload a persisted snapshot from the snapshot store
    /// - Normalize and validate the snapshot before restore
    /// - Restore record and state into the runtime store
    /// - Support idempotent replay when a compatible runtime execution already exists
    ///
    /// IMPORTANT:
    /// - In distributed DAG mode, the authoritative execution truth lives in IAiDagExecutionStore
    /// - In non-distributed / generic mode, the replay contract uses IAiExecutionStore
    /// - This service therefore checks both stores when needed
    /// </summary>
    public sealed class DefaultAiExecutionReplayService<TContext> : IAiExecutionReplayService
    {
        private readonly IAiExecutionSnapshotStore<TContext> _snapshotStore;
        private readonly IAiExecutionStore _executionStore;
        private readonly IAiExecutionSnapshotService<TContext> _snapshotService;
        private readonly IAiRuntimeLogger _logger;
        private readonly IAiDagExecutionStore? _dagStore;

        /// <summary>
        /// Initializes a new replay service instance.
        /// </summary>
        public DefaultAiExecutionReplayService(
            IAiExecutionSnapshotStore<TContext> snapshotStore,
            IAiExecutionStore executionStore,
            IAiExecutionSnapshotService<TContext> snapshotService,
            IAiRuntimeLogger logger,
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

            _dagStore = dagStore;
        }

        /// <summary>
        /// Replays a persisted execution snapshot back into the runtime store.
        /// </summary>
        public async Task<AiExecutionReplayReport> ReplayAsync(
            AiExecutionReplayRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.ExecutionId))
            {
                throw new ArgumentException(
                    "ExecutionId cannot be null or empty.",
                    nameof(request.ExecutionId));
            }

            var executionId = request.ExecutionId;

            var replayedAtUtc = DateTime.UtcNow;

            var snapshot = await _snapshotStore.GetAsync(
                executionId,
                cancellationToken).ConfigureAwait(false);

            if (snapshot is null)
            {
                _logger.Engine.ExecutionReplaySkipped(
                    executionId,
                    "Snapshot not found.");

                return new AiExecutionReplayReport
                {
                    ExecutionId = executionId,
                    Mode = request.Mode,
                    ExecutionFound = false,
                    SnapshotFound = false,
                    ReplayValid = false,
                    FailureReason = "Snapshot not found."
                };
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

                return new AiExecutionReplayReport
                {
                    ExecutionId = executionId,
                    Mode = request.Mode,
                    ExecutionFound = true,
                    SnapshotFound = true,
                    ReplayValid = false,
                    FailureReason = validationError
                };
            }

            var existingRecord = await GetExistingRecordAsync(
                executionId,
                cancellationToken).ConfigureAwait(false);

            if (existingRecord is not null &&
                IsCompatibleExistingRecord(
                    existingRecord,
                    snapshot.Record!))
            {
                _logger.Engine.ExecutionReplaySkipped(
                    executionId,
                    "Replay skipped because a compatible runtime execution already exists.");

                return new AiExecutionReplayReport
                {
                    ExecutionId = executionId,
                    Mode = request.Mode,
                    ExecutionFound = true,
                    SnapshotFound = true,
                    ReplayValid = true,
                    PipelineName = existingRecord.PipelineName,
                    Status = existingRecord.Status.ToString(),
                    TotalSteps = snapshot.State?.Steps?.Count ?? 0
                };
            }

            if (request.Mode == AiExecutionReplayMode.AuditOnly)
            {
                return new AiExecutionReplayReport
                {
                    ExecutionId = executionId,
                    Mode = request.Mode,
                    ExecutionFound = true,
                    SnapshotFound = true,
                    ReplayValid = true,
                    PipelineName = snapshot.Record!.PipelineName,
                    Status = snapshot.Record.Status.ToString(),
                    TotalSteps = snapshot.State?.Steps?.Count ?? 0
                };
            }

            AiExecutionReplayPreparation.Prepare(
                snapshot.Record,
                snapshot.State);

            await RestoreToAuthoritativeStoreAsync(
                snapshot.Record!,
                snapshot.State!,
                cancellationToken).ConfigureAwait(false);

            await _snapshotService.TryPersistAsync(
                snapshot.Record!,
                snapshot.State!,
                snapshot.ContextKey,
                snapshot.ContextSnapshot,
                cancellationToken).ConfigureAwait(false);

            _logger.Engine.ExecutionReplayRestored(
                executionId,
                snapshot.Record!.Status,
                snapshot.State?.Steps?.Count ?? 0);

            return new AiExecutionReplayReport
            {
                ExecutionId = executionId,
                Mode = request.Mode,
                ExecutionFound = true,
                SnapshotFound = true,
                ReplayValid = true,
                PipelineName = snapshot.Record.PipelineName,
                Status = snapshot.Record.Status.ToString(),
                TotalSteps = snapshot.State?.Steps?.Count ?? 0,
                CompletedSteps = snapshot.State?.Steps?.Values.Count(
                    x => string.Equals(
                        x.Status.ToString(),
                        "Completed",
                        StringComparison.OrdinalIgnoreCase)) ?? 0
            };
        }

        /// <summary>
        /// Gets an existing runtime record from the authoritative DAG store when available,
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
                    cancellationToken).ConfigureAwait(false);

                if (dagRecord is not null)
                {
                    return dagRecord;
                }
            }

            return await _executionStore.GetRecordAsync(
                executionId,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Restores the snapshot into the authoritative runtime store.
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
                    cancellationToken).ConfigureAwait(false);

                return;
            }

            await _executionStore.RestoreAsync(
                record,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that the persisted snapshot contains the minimum data required
        /// for a safe replay into the runtime store.
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
        /// Determines whether an already existing runtime record is compatible
        /// with the snapshot targeted by replay.
        /// </summary>
        private static bool IsCompatibleExistingRecord(
            AiExecutionRecord existingRecord,
            AiExecutionRecord snapshotRecord)
        {
            if (!string.Equals(
                    existingRecord.ExecutionId,
                    snapshotRecord.ExecutionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(
                    existingRecord.PipelineName,
                    snapshotRecord.PipelineName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(
                    existingRecord.ContextKey,
                    snapshotRecord.ContextKey,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}