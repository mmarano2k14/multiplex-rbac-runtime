using Multiplexed.Abstractions.AI.Execution.Instance.Worker;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// No-op implementation of <see cref="IAiRuntimePipelineRunLifecycleHook"/>.
    /// </summary>
    public sealed class NoOpAiRuntimePipelineRunLifecycleHook
        : IAiRuntimePipelineRunLifecycleHook
    {
        /// <inheritdoc />
        public Task OnFinalizedAsync(
            AiRuntimePipelineRunFinalizedContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            return Task.CompletedTask;
        }
    }
}