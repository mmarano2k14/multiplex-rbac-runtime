using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution.Payloads
{
    public interface IAiStepResultPayloadCompactor
    {
        Task CompactAsync(
            AiStepResult result,
            CancellationToken cancellationToken = default);
    }
}