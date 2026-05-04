using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Test
{
    /// <summary>
    /// Debug step used only for distributed retry budget tests.
    ///
    /// This step always throws so the distributed DAG runtime can decide whether
    /// the step should be retried or failed terminally according to RetryCount
    /// and MaxRetries persisted in step state.
    /// </summary>
    [AiStep("debug-retry-fail")]
    public sealed class DebugRetryFailStep : IAiStep
    {
        public string Name => "debug-retry-fail";

        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var stepState = context.Execution.StateWriter.GetOrCreateStep(context.Execution.State, context.Step.Name);

            throw new InvalidOperationException(
                $"Intentional retry test failure. " +
                $"Step='{context.Step.Name}', " +
                $"RetryCount={stepState.RetryState?.RetryCount ?? 0}, " +
                $"MaxRetries={stepState.Retry?.MaxRetries ?? 0}, " +
                $"Status={stepState.Status}, " +
                $"NextRetryAtUtc={stepState.RetryState?.NextRetryAtUtc?.ToString("O") ?? "null"}.");
        }
    }
}