using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    /// <summary>
    /// Responsible for cleaning up a full AI execution bundle.
    ///
    /// This includes:
    /// - Execution record
    /// - Execution state
    /// - DAG distributed steps (if applicable)
    /// - Optional snapshot cleanup
    /// - Optional AI-owned RBAC cleanup
    ///
    /// DESIGN:
    /// - Idempotent
    /// - Safe for retries
    /// - Partial failure tolerant
    /// - Orchestrates specialized cleanup services
    /// </summary>
    public sealed class AiExecutionCleanupService : IAiExecutionCleanupService
    {
        private readonly IAiExecutionStore _executionStore;
        private readonly IAiDagExecutionStore _dagStore;
        private readonly IAiExecutionSnapshotCleanupService _snapshotCleanupService;
        private readonly IAiOwnedRbacCleanupService _ownedRbacCleanupService;
        private readonly IAiRuntimeLogger _logger;

        public AiExecutionCleanupService(
            IAiExecutionStore executionStore,
            IAiDagExecutionStore dagStore,
            IAiExecutionSnapshotCleanupService snapshotCleanupService,
            IAiOwnedRbacCleanupService ownedRbacCleanupService,
            IAiRuntimeLogger logger)
        {
            _executionStore = executionStore ?? throw new ArgumentNullException(nameof(executionStore));
            _dagStore = dagStore ?? throw new ArgumentNullException(nameof(dagStore));
            _snapshotCleanupService = snapshotCleanupService ?? throw new ArgumentNullException(nameof(snapshotCleanupService));
            _ownedRbacCleanupService = ownedRbacCleanupService ?? throw new ArgumentNullException(nameof(ownedRbacCleanupService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Entry point for cleanup orchestration.
        /// </summary>
        public async Task CleanupAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            await DeleteExecutionBundleAsync(executionId, cancellationToken);
        }

        /// <summary>
        /// Cleans up an execution bundle using its execution identifier.
        ///
        /// BEHAVIOR:
        /// - Attempts to load the execution record first.
        /// - If found, cleanup uses the richer record-based path.
        /// - If not found, cleanup falls back to executionId-only cleanup.
        ///
        /// WHY:
        /// - Preserves idempotency when the record was already deleted.
        /// - Ensures state, DAG artifacts, snapshots, and AI-owned RBAC resources
        ///   can still be cleaned up.
        /// </summary>
        public async Task<bool> DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("ExecutionId cannot be null or empty.", nameof(executionId));
            }

            _logger.Engine.LogInformation($"[CLEANUP] Starting cleanup for execution '{executionId}'");

            try
            {
                var record = await _executionStore.GetRecordAsync(executionId, cancellationToken);

                if (record is not null)
                {
                    return await DeleteExecutionBundleAsync(record, cancellationToken);
                }

                _logger.Engine.LogWarning(
                    $"[CLEANUP] Execution '{executionId}' record not found. Falling back to execution-id cleanup.");

                return await DeleteExecutionCoreAsync(
                    executionId: executionId,
                    contextKey: null,
                    shouldDeleteDagBundle: true,
                    shouldDeleteRecord: false,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Engine.LogError(ex, $"[CLEANUP] Fatal cleanup error for '{executionId}'");
                throw;
            }
        }

        /// <summary>
        /// Cleans up an execution bundle using a fully loaded execution record.
        ///
        /// This is the preferred path because it provides:
        /// - Execution mode
        /// - Context key
        /// - Full execution metadata
        /// </summary>
        public async Task<bool> DeleteExecutionBundleAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            return await DeleteExecutionCoreAsync(
                executionId: record.ExecutionId,
                contextKey: record.ContextKey,
                shouldDeleteDagBundle: record.ExecutionMode == AiExecutionMode.Dag,
                shouldDeleteRecord: true,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Central cleanup implementation shared by both public overloads.
        ///
        /// PURPOSE:
        /// - Avoid duplicated cleanup logic between executionId-based and record-based paths.
        /// - Keep cleanup ordering consistent across all entry points.
        ///
        /// ORDER:
        /// - DAG bundle cleanup (optional)
        /// - Execution state cleanup
        /// - Execution record cleanup (optional)
        /// - Snapshot cleanup
        /// - AI-owned RBAC cleanup
        /// </summary>
        private async Task<bool> DeleteExecutionCoreAsync(
            string executionId,
            string? contextKey,
            bool shouldDeleteDagBundle,
            bool shouldDeleteRecord,
            CancellationToken cancellationToken = default)
        {
            var deletedSomething = false;

            _logger.Engine.LogInformation($"[CLEANUP] Starting cleanup for execution '{executionId}'");

            try
            {
                // -------------------------
                // DAG CLEANUP
                // -------------------------
                if (shouldDeleteDagBundle)
                {
                    try
                    {
                        _logger.Engine.LogInformation($"[CLEANUP] Deleting DAG execution bundle for '{executionId}'");

                        await _dagStore.DeleteExecutionBundleAsync(executionId, cancellationToken);
                        deletedSomething = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.Engine.LogError(ex, $"[CLEANUP] Failed to cleanup DAG steps for '{executionId}'");
                    }
                }

                // -------------------------
                // EXECUTION STATE CLEANUP
                // -------------------------
                try
                {
                    _logger.Engine.LogInformation($"[CLEANUP] Deleting execution state for '{executionId}'");

                    await _executionStore.DeleteStateAsync(executionId, cancellationToken);
                    deletedSomething = true;
                }
                catch (Exception ex)
                {
                    _logger.Engine.LogError(ex, $"[CLEANUP] Failed to delete state for '{executionId}'");
                }

                // -------------------------
                // EXECUTION RECORD CLEANUP
                // -------------------------
                if (shouldDeleteRecord)
                {
                    try
                    {
                        _logger.Engine.LogInformation($"[CLEANUP] Deleting execution record for '{executionId}'");

                        await _executionStore.DeleteRecordAsync(executionId, cancellationToken);
                        deletedSomething = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.Engine.LogError(ex, $"[CLEANUP] Failed to delete record for '{executionId}'");
                    }
                }

                // -------------------------
                // SNAPSHOT CLEANUP (optional)
                // -------------------------
                try
                {
                    var snapshotCleanupResult = await _snapshotCleanupService.DeleteSnapshotAsync(
                        executionId,
                        cancellationToken);

                    if (snapshotCleanupResult.SnapshotDeleted)
                    {
                        deletedSomething = deletedSomething || snapshotCleanupResult.SnapshotFound;
                    }

                    if (snapshotCleanupResult.Errors.Count > 0)
                    {
                        _logger.Engine.LogWarning(
                            $"[CLEANUP] Snapshot cleanup completed with errors for '{executionId}': {string.Join(" | ", snapshotCleanupResult.Errors)}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Engine.LogError(ex, $"[CLEANUP] Failed to cleanup snapshot for '{executionId}'");
                }

                // -------------------------
                // AI-OWNED RBAC CLEANUP
                // -------------------------
                try
                {
                    var rbacCleanupResult = await _ownedRbacCleanupService.DeleteOwnedResourcesAsync(
                        executionId,
                        contextKey,
                        cancellationToken);

                    if (rbacCleanupResult.ContextFound)
                    {
                        deletedSomething = true;
                    }

                    if (rbacCleanupResult.Errors.Count > 0)
                    {
                        _logger.Engine.LogWarning(
                            $"[CLEANUP] RBAC cleanup completed with errors for '{executionId}': {string.Join(" | ", rbacCleanupResult.Errors)}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Engine.LogError(ex, $"[CLEANUP] Failed to cleanup RBAC resources for '{executionId}'");
                }

                _logger.Engine.LogInformation($"[CLEANUP] Completed cleanup for execution '{executionId}'");

                return deletedSomething;
            }
            catch (Exception ex)
            {
                _logger.Engine.LogError(ex, $"[CLEANUP] Fatal cleanup error for '{executionId}'");
                throw;
            }
        }
    }
}