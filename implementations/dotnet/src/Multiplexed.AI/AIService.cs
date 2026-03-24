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

        public Task<AIResponse> CompleteAsync(
            AIRequest request,
            CancellationToken cancellationToken = default)
        {
            return _provider.CompleteAsync(request, cancellationToken);
        }
    }
}