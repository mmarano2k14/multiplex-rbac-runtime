using Multiplexed.Abstractions.AI.Execution.Cleanup;


namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    public sealed class NoopAiOwnedResourceLocator : IAiOwnedResourceLocator
    {
        public Task<IReadOnlyList<string>> GetOwnedResourceKeysAsync(string executionId, string? contextKey, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
