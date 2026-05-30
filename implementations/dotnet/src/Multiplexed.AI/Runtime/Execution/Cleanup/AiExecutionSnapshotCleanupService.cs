using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    public sealed class AiExecutionSnapshotCleanupService : IAiExecutionSnapshotCleanupService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AiEngineOptions _options;
        private readonly ILogger<AiExecutionSnapshotCleanupService> _logger;

        public AiExecutionSnapshotCleanupService(
            IServiceProvider serviceProvider,
            IOptions<AiEngineOptions> options,
            ILogger<AiExecutionSnapshotCleanupService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AiExecutionSnapshotCleanupResult> DeleteSnapshotAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            var warnings = new List<string>();
            var errors = new List<string>();

            try
            {
                if (!_options.Cleanup.SuppressSnapshotIfExist)
                {
                    warnings.Add("Snapshot cleanup disabled by configuration.");

                    return new AiExecutionSnapshotCleanupResult
                    {
                        SnapshotFound = false,
                        SnapshotDeleted = true,
                        Warnings = warnings,
                        Errors = errors
                    };
                }

                var snapshotStore =
                    _serviceProvider.GetService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

                if (snapshotStore is null)
                {
                    warnings.Add("No snapshot store is registered.");

                    return new AiExecutionSnapshotCleanupResult
                    {
                        SnapshotFound = false,
                        SnapshotDeleted = true,
                        Warnings = warnings,
                        Errors = errors
                    };
                }

                var snapshot = await snapshotStore.GetAsync(executionId);

                if (snapshot is null)
                {
                    return new AiExecutionSnapshotCleanupResult
                    {
                        SnapshotFound = false,
                        SnapshotDeleted = true,
                        Warnings = warnings,
                        Errors = errors
                    };
                }

                await snapshotStore.DeleteAsync(executionId);

                return new AiExecutionSnapshotCleanupResult
                {
                    SnapshotFound = true,
                    SnapshotDeleted = true,
                    Warnings = warnings,
                    Errors = errors
                };
            }
            catch (Exception ex)
            {
                errors.Add($"Snapshot cleanup failed: {ex.Message}");

                _logger.LogError(
                    ex,
                    "AiExecutionSnapshotCleanupFailed ExecutionId={ExecutionId}",
                    executionId);

                return new AiExecutionSnapshotCleanupResult
                {
                    SnapshotFound = false,
                    SnapshotDeleted = false,
                    Warnings = warnings,
                    Errors = errors
                };
            }
        }
    }
}