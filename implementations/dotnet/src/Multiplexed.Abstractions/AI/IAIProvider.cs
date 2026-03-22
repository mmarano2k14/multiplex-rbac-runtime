namespace Multiplexed.Abstractions.AI
{
    public interface IAIProvider
    {
        Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
    }
}