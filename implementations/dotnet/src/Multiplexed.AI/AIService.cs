using Multiplexed.Abstractions.AI;
using Multiplexed.AI.Abstractions;

namespace Multiplexed.AI
{
    public sealed class AiService : IAiService
    {
        private readonly IAiProvider _provider;

        public AiService(IAiProvider provider)
        {
            _provider = provider;
        }

        public Task<AiResponse> CompleteAsync(
            AiRequest request,
            CancellationToken cancellationToken = default)
        {
            return _provider.CompleteAsync(request, cancellationToken);
        }
    }
}