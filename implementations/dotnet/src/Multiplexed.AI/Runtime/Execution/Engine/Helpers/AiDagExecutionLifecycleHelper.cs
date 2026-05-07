using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution.Engine.Helpers
{
    /// <summary>
    /// Handles terminal execution lifecycle side effects such as snapshot persistence
    /// and automatic cleanup.
    /// </summary>
    public sealed class AiDagExecutionLifecycleHelper
    {
        private readonly IAiDagExecutionEngineServices _engineServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionLifecycleHelper"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services.
        /// </param>
        public AiDagExecutionLifecycleHelper(
            IAiDagExecutionEngineServices engineServices)
        {
            _engineServices = engineServices
                ?? throw new ArgumentNullException(nameof(engineServices));
        }

        /// <summary>
        /// Attempts to persist a durable terminal execution snapshot.
        /// </summary>
        /// <param name="record">
        /// The terminal execution record.
        /// </param>
        /// <param name="state">
        /// The authoritative execution state.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task TryPersistTerminalSnapshotAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (!record.IsTerminal)
            {
                return;
            }

            if (!_engineServices.AiOptions.Value.Snapshots.Enabled)
            {
                return;
            }

            if (_engineServices.SnapshotService is null)
            {
                return;
            }

            try
            {
                ExecutionContextSnapshot? contextSnapshot = null;

                if (_engineServices.Accessor.Current is not null)
                {
                    contextSnapshot = _engineServices.ContextFactory.CreateSnapshot(
                        _engineServices.Accessor.Current);
                }

                await _engineServices.SnapshotService.TryPersistAsync(
                    record,
                    state,
                    record.ContextKey,
                    contextSnapshot,
                    cancellationToken);

                _engineServices.Logger.Engine.SnapshotPersisted(
                    record.ExecutionId,
                    record.Status);

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI SNAPSHOT] Persisted terminal snapshot for execution '{record.ExecutionId}' with status '{record.Status}'.");
            }
            catch (Exception ex)
            {
                _engineServices.Logger.Engine.LogError(
                    ex,
                    $"[AI SNAPSHOT] Failed for execution '{record.ExecutionId}'.");
            }
        }

        /// <summary>
        /// Attempts automatic cleanup when configured and when the execution is terminal.
        /// </summary>
        /// <param name="record">
        /// The execution record.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task TryCleanupIfNeededAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (!record.IsTerminal)
            {
                _engineServices.Logger.Engine.CleanupSkipped(
                    record.ExecutionId,
                    "Execution is not terminal.");

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' because the execution is not terminal.");

                return;
            }

            var shouldCleanup =
                (record.Status == AiExecutionStatus.Completed &&
                 _engineServices.AiOptions.Value.Cleanup.AutoCleanupOnCompleted) ||
                (record.Status == AiExecutionStatus.Failed &&
                 _engineServices.AiOptions.Value.Cleanup.AutoCleanupOnFailed);

            if (!shouldCleanup)
            {
                _engineServices.Logger.Engine.CleanupSkipped(
                    record.ExecutionId,
                    "Automatic cleanup is disabled for the current terminal status.");

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' with status '{record.Status}' because automatic cleanup is disabled.");

                return;
            }

            _engineServices.Logger.Engine.CleanupStarted(
                record.ExecutionId,
                record.Status);

            _engineServices.Logger.Engine.LogInformation(
                $"[AI CLEANUP] Starting for execution '{record.ExecutionId}' with status '{record.Status}'.");

            try
            {
                await _engineServices.CleanupService.DeleteExecutionBundleAsync(
                    record.ExecutionId,
                    cancellationToken);

                _engineServices.Logger.Engine.CleanupCompleted(
                    record.ExecutionId);

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Completed for execution '{record.ExecutionId}'.");
            }
            catch (Exception ex)
            {
                _engineServices.Logger.Engine.LogError(
                    ex,
                    $"[AI CLEANUP] Failed for execution '{record.ExecutionId}'.");

                if (!_engineServices.AiOptions.Value.Cleanup.SuppressCleanupExceptions)
                {
                    throw;
                }
            }
        }
    }
}