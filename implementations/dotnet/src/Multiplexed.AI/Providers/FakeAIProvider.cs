using Multiplexed.Abstractions.AI;

namespace Multiplexed.AI.Providers
{
    public sealed class FakeAIProvider : IAiProvider
    {
        public Task<AiResponse> CompleteAsync(
            AiRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Content = $"[FAKE AI RESPONSE] {request.Prompt}",
                Model = "fake",
                Duration = TimeSpan.FromMilliseconds(10)
            });
        }
    }
}