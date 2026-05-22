using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Execution.Persistence;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos
{
    /// <summary>
    /// Captures the finalized lifecycle event for distributed chaos executions.
    /// </summary>
    public sealed class DistributedChaosRunFinalizedHook
        : IAiRuntimePipelineRunLifecycleHook
    {
        private readonly TaskCompletionSource<AiRuntimePipelineRunFinalizedContext> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Waits until a distributed chaos execution has been finalized.
        /// </summary>
        /// <param name="timeout">
        /// The wait timeout.
        /// </param>
        /// <returns>
        /// The finalized run context.
        /// </returns>
        public async Task<AiRuntimePipelineRunFinalizedContext> WaitAsync(
            TimeSpan timeout)
        {
            return await _completion.Task.WaitAsync(
                    timeout)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task OnFinalizedAsync(
            AiRuntimePipelineRunFinalizedContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                context);

            _completion.TrySetResult(
                context);

            return Task.CompletedTask;
        }
    }
}