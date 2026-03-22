using Multiplexed.Abstractions.AI;
using Multiplexed.AI.Abstractions;

namespace Multiplexed.AI
{
    public sealed class AIService : IAIService
    {
        private readonly IAIProvider _provider;

        public AIService(IAIProvider provider)
        {
            _provider = provider;
        }

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return _provider.CompleteAsync(prompt, cancellationToken);
        }
    }
}