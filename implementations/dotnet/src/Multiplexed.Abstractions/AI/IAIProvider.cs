namespace Multiplexed.Abstractions.AI
{
    public interface IAIProvider
    {
        Task<AIResponse> CompleteAsync(
            AIRequest request,
            CancellationToken cancellationToken = default);
    }
}