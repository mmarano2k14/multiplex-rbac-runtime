using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.Execution.Cleanup;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    public sealed class AiOwnedRbacCleanupService : IAiOwnedRbacCleanupService
    {
        private readonly IAiOwnedResourceLocator _resourceLocator;
        private readonly IAiOwnedResourceDeleter _resourceDeleter;
        private readonly ILogger<AiOwnedRbacCleanupService> _logger;

        public AiOwnedRbacCleanupService(
            IAiOwnedResourceLocator resourceLocator,
            IAiOwnedResourceDeleter resourceDeleter,
            ILogger<AiOwnedRbacCleanupService> logger)
        {
            _resourceLocator = resourceLocator ?? throw new ArgumentNullException(nameof(resourceLocator));
            _resourceDeleter = resourceDeleter ?? throw new ArgumentNullException(nameof(resourceDeleter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AiOwnedRbacCleanupResult> DeleteOwnedResourcesAsync(
            string executionId,
            string? contextKey,
            CancellationToken cancellationToken = default)
        {
            var warnings = new List<string>();
            var errors = new List<string>();

            try
            {
                var ownedKeys = await _resourceLocator.GetOwnedResourceKeysAsync(
                    executionId,
                    contextKey,
                    cancellationToken);

                var found = ownedKeys.Count > 0;

                if (!found)
                {
                    return new AiOwnedRbacCleanupResult
                    {
                        ContextFound = false,
                        ContextDeleted = true,
                        Warnings = warnings,
                        Errors = errors
                    };
                }

                var deletedCount = await _resourceDeleter.DeleteOwnedResourceKeysAsync(
                    ownedKeys,
                    cancellationToken);

                return new AiOwnedRbacCleanupResult
                {
                    ContextFound = true,
                    ContextDeleted = deletedCount == ownedKeys.Count,
                    Warnings = warnings,
                    Errors = errors
                };
            }
            catch (Exception ex)
            {
                errors.Add($"RBAC cleanup failed: {ex.Message}");

                _logger.LogError(
                    ex,
                    "AiOwnedRbacCleanupFailed ExecutionId={ExecutionId} ContextKey={ContextKey}",
                    executionId,
                    contextKey);

                return new AiOwnedRbacCleanupResult
                {
                    ContextFound = false,
                    ContextDeleted = false,
                    Warnings = warnings,
                    Errors = errors
                };
            }
        }
    }
}