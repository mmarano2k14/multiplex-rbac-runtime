using Multiplexed.Abstractions.AI;

namespace Multiplexed.AI.Abstractions
{
    public interface IAIService
    {
        Task<AIResponse> CompleteAsync(
            AIRequest request,
            CancellationToken cancellationToken = default);
    }
}