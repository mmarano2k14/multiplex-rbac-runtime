using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Test
{
    /// <summary>
    /// Step that exposes retry/debug info.
    /// </summary>
    [AiStep("debug-retry")]
    public sealed class DebugRetryStep : IAiStep
    {
        public string Name => "debug-retry";

        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var stepState = context.Execution.StateWriter.GetOrCreateStep(context.Execution.State, context.Step.Name);

            return Task.FromResult(
                AiStepResult.Ok(
                    output: $"RetryCount={stepState.RetryState?.RetryCount ?? 0}",
                    data: new Dictionary<string, object?>
                    {
                        ["retryCount"] = context.StepState.RetryState?.RetryCount ?? 0,
                        ["status"] = context.StepState.Status.ToString(),
                        ["nextRetry"] = context.StepState.RetryState?.NextRetryAtUtc
                    }));
        }
    }
}