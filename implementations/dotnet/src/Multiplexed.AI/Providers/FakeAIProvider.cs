using Multiplexed.Abstractions.AI;

namespace Multiplexed.AI.Providers
{
    public sealed class FakeAIProvider : IAIProvider
    {
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"[FAKE AI RESPONSE] {prompt}");
        }
    }
}