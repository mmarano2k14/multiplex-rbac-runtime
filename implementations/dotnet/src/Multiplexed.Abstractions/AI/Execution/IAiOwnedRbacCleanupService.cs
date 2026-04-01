namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Deletes RBAC resources owned by an AI execution.
    /// </summary>
    public interface IAiOwnedRbacCleanupService
    {
        Task<AiOwnedRbacCleanupResult> DeleteOwnedResourcesAsync(
            string executionId,
            string? contextKey,
            CancellationToken cancellationToken = default);
    }

    public sealed class AiOwnedRbacCleanupResult
    {
        public bool ContextFound { get; init; }
        public bool ContextDeleted { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }
}