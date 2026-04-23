using Multiplexed.Abstractions.AI.Execution.Cleanup;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    /// <summary>
    /// Temporary no-op RBAC cleanup service used until a real AI-owned RBAC cleanup
    /// implementation is plugged into the runtime.
    /// </summary>
    public sealed class NoOpAiOwnedRbacCleanupService : IAiOwnedRbacCleanupService
    {
        public Task<AiOwnedRbacCleanupResult> DeleteOwnedResourcesAsync(
            string executionId,
            string? contextKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiOwnedRbacCleanupResult
            {
                ContextFound = false,
                ContextDeleted = true,
                Warnings = Array.Empty<string>(),
                Errors = Array.Empty<string>()
            });
        }
    }
}