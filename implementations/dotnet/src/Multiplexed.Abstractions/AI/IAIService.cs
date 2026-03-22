namespace Multiplexed.AI.Abstractions
{
    public interface IAIService
    {
        Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
    }
}