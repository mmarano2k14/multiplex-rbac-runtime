namespace Multiplexed.Abstractions.AI
{
    public interface IAiProvider
    {
        Task<AiResponse> CompleteAsync(
            AiRequest request,
            CancellationToken cancellationToken = default);
    }
}