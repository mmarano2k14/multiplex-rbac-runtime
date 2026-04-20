using Multiplexed.Abstractions.AI.Plugins;

namespace Multiplexed.Abstractions.AI.Steps.External
{
    public interface IExternalAiStep<TContextSnapshot> : IExternalAiStep
    {
        Task<object?> ExecuteAsync(
            IPluginExecutionContext<TContextSnapshot> context,
            CancellationToken cancellationToken);
    }
}