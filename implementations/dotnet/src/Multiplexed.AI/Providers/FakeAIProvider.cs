using Multiplexed.Abstractions.AI;

namespace Multiplexed.AI.Providers
{
    public sealed class FakeAIProvider : IAIProvider
    {
        public Task<AIResponse> CompleteAsync(
            AIRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AIResponse
            {
                Content = $"[FAKE AI RESPONSE] {request.Prompt}",
                Model = "fake",
                Duration = TimeSpan.FromMilliseconds(10)
            });
        }
    }
}