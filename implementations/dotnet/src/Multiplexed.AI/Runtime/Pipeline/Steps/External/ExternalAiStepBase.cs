using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Steps.External;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.External
{
    /// <summary>
    /// Base class for strongly typed external steps.
    /// </summary>
    public abstract class ExternalAiStepBase<TContextSnapshot>
        : IExternalAiStep<TContextSnapshot>
    {
        public abstract string StepType { get; }

        public Type ExecutionContextType => typeof(TContextSnapshot);

        public abstract Task<object?> ExecuteAsync(
            IPluginExecutionContext<TContextSnapshot> context,
            CancellationToken cancellationToken);

        public async Task<object?> ExecuteUntypedAsync(
            object context,
            CancellationToken cancellationToken)
        {
            if (context is not IPluginExecutionContext<TContextSnapshot> typed)
            {
                throw new InvalidOperationException(
                    $"Step '{StepType}' expects context '{typeof(TContextSnapshot).Name}'.");
            }

            return await ExecuteAsync(typed, cancellationToken).ConfigureAwait(false);
        }
    }
}