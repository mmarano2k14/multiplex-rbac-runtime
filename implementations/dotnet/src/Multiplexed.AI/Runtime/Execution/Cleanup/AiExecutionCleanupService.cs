using Multiplexed.Abstractions.AI.Execution;
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
    /// - Future: RBAC context cleanup
    ///
    /// DESIGN:
    /// - Idempotent
    /// - Safe for retries
    /// - Partial failure tolerant
    /// </summary>
    public sealed class AiExecutionCleanupService : IAiExecutionCleanupService
    {
        private readonly IAiExecutionStore _executionStore;
        private readonly IAiDagExecutionStore _dagStore;
        private readonly IAiRuntimeLogger _logger;

        public AiExecutionCleanupService(
            IAiExecutionStore executionStore,
            IAiDagExecutionStore dagStore,
            IAiRuntimeLogger logger)
        {
            _executionStore = executionStore ?? throw new ArgumentNullException(nameof(executionStore));
            _dagStore = dagStore ?? throw new ArgumentNullException(nameof(dagStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CleanupAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            await DeleteExecutionBundleAsync(executionId, cancellationToken);
        }

        public async Task<bool> DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("ExecutionId cannot be null or empty.", nameof(executionId));

            _logger.Engine.LogInformation($"[CLEANUP] Starting cleanup for execution '{executionId}'");

            try
            {
                var record = await _executionStore.GetRecordAsync(executionId, cancellationToken);

                if (record == null)
                {
                    _logger.Engine.LogWarning($"[CLEANUP] Execution '{executionId}' not found. Skipping cleanup.");
                    return false;
                }

                return await DeleteExecutionBundleAsync(record, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Engine.LogError(ex, $"[CLEANUP] Fatal cleanup error for '{executionId}'");
                throw;
            }
        }

        public async Task<bool> DeleteExecutionBundleAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            var executionId = record.ExecutionId;
            var deletedSomething = false;

            _logger.Engine.LogInformation($"[CLEANUP] Starting cleanup for execution '{executionId}'");

            try
            {
                // -------------------------
                // DAG CLEANUP (if needed)
                // -------------------------
                if (record.ExecutionMode == AiExecutionMode.Dag)
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

                // -------------------------
                // FUTURE: RBAC CLEANUP
                // -------------------------
                // await _rbacCleanupService.DeleteContextAsync(...);

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