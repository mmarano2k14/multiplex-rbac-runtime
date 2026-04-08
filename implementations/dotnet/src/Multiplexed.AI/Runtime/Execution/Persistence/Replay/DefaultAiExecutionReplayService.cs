using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.AI.Runtime.Execution.Persistence.Normalization;
using Multiplexed.AI.Stores;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Default replay service that restores a persisted execution snapshot
    /// back into the runtime execution store.
    /// </summary>
    public sealed class DefaultAiExecutionReplayService<TContext> : IAiExecutionReplayService
    {
        private readonly IAiExecutionSnapshotStore<TContext> _snapshotStore;
        private readonly IAiExecutionStore _executionStore;
        private readonly IAiExecutionSnapshotService<TContext> _snapshotService;

        public DefaultAiExecutionReplayService(
            IAiExecutionSnapshotStore<TContext> snapshotStore,
            IAiExecutionStore executionStore,
            IAiExecutionSnapshotService<TContext> snapshotService)
        {
            _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
            _executionStore = executionStore ?? throw new ArgumentNullException(nameof(executionStore));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        }

        public async Task<AiExecutionReplayResult> ReplayAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("ExecutionId cannot be null or empty.", nameof(executionId));
            }

            var snapshot = await _snapshotStore.GetAsync(executionId, cancellationToken);

            if (snapshot is null)
            {
                return new AiExecutionReplayResult
                {
                    ExecutionId = executionId,
                    SnapshotFound = false,
                    IsValid = false,
                    Restored = false,
                    ReplayPerformedAtUtc = DateTime.UtcNow,
                    Message = "Snapshot not found."
                };
            }

            AiExecutionSnapshotRemapper.Remap(snapshot);

            var validationError = ValidateSnapshot(snapshot);
            if (validationError is not null)
            {
                return new AiExecutionReplayResult
                {
                    ExecutionId = executionId,
                    SnapshotFound = true,
                    IsValid = false,
                    Restored = false,
                    ReplayPerformedAtUtc = DateTime.UtcNow,
                    Message = validationError
                };
            }

            var existingRecord = await _executionStore.GetRecordAsync(executionId, cancellationToken);

            if (existingRecord is not null && IsCompatibleExistingRecord(existingRecord, snapshot.Record!))
            {
                return new AiExecutionReplayResult
                {
                    ExecutionId = executionId,
                    SnapshotFound = true,
                    AlreadyExists = true,
                    IsValid = true,
                    Restored = false,
                    ExistingStatus = existingRecord.Status,
                    RestoredStatus = snapshot.Record!.Status,
                    ReplayPerformedAtUtc = DateTime.UtcNow,
                    StepsCount = snapshot.State?.Steps?.Count ?? 0,
                    Message = "Execution already exists in a compatible state. Replay skipped."
                };
            }

            AiExecutionReplayPreparation.Prepare(snapshot.Record, snapshot.State);

            await _executionStore.RestoreAsync(
                snapshot.Record!,
                snapshot.State!,
                cancellationToken);

            await _snapshotService.TryPersistAsync(
                snapshot.Record!,
                snapshot.State!,
                snapshot.ContextKey,
                snapshot.ContextSnapshot,
                cancellationToken);

            return new AiExecutionReplayResult
            {
                ExecutionId = executionId,
                SnapshotFound = true,
                AlreadyExists = existingRecord is not null,
                IsValid = true,
                Restored = true,
                ExistingStatus = existingRecord?.Status,
                RestoredStatus = snapshot.Record.Status,
                Status = snapshot.Record.Status,
                StepsCount = snapshot.State?.Steps?.Count ?? 0,
                ReplayPerformedAtUtc = DateTime.UtcNow,
                Message = "Execution restored successfully from snapshot."
            };
        }

        private static string? ValidateSnapshot(AiExecutionSnapshotDocument<TContext> snapshot)
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

            if (!string.Equals(snapshot.ExecutionId, snapshot.Record.ExecutionId, StringComparison.Ordinal))
            {
                return "Snapshot execution id does not match record execution id.";
            }

            if (!string.Equals(snapshot.ExecutionId, snapshot.State.ExecutionId, StringComparison.Ordinal))
            {
                return "Snapshot execution id does not match state execution id.";
            }

            if (!string.Equals(snapshot.Record.ExecutionId, snapshot.State.ExecutionId, StringComparison.Ordinal))
            {
                return "Record execution id does not match state execution id.";
            }

            if (string.IsNullOrWhiteSpace(snapshot.Record.PipelineName))
            {
                return "Snapshot record pipeline name is missing.";
            }

            return null;
        }

        private static bool IsCompatibleExistingRecord(
            AiExecutionRecord existingRecord,
            AiExecutionRecord snapshotRecord)
        {
            if (!string.Equals(existingRecord.ExecutionId, snapshotRecord.ExecutionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(existingRecord.PipelineName, snapshotRecord.PipelineName, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}