namespace Multiplexed.Abstractions.AI.Execution
{
    public interface IAiOwnedResourceLocator
    {
        Task<IReadOnlyList<string>> GetOwnedResourceKeysAsync(
            string executionId,
            string? contextKey,
            CancellationToken cancellationToken = default);
    }

    public interface IAiOwnedResourceDeleter
    {
        Task<int> DeleteOwnedResourceKeysAsync(
            IReadOnlyList<string> resourceKeys,
            CancellationToken cancellationToken = default);
    }
}