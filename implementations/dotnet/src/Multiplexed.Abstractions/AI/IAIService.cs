using Multiplexed.Abstractions.AI;

namespace Multiplexed.AI.Abstractions
{
    public interface IAiService
    {
        Task<AiResponse> CompleteAsync(
            AiRequest request,
            CancellationToken cancellationToken = default);
    }
}