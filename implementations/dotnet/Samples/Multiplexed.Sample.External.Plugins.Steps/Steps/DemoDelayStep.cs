using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;

namespace Multiplexed.Sample.External.Plugins.Steps.Steps
{
    /// <summary>
    /// External demo step that waits for a configurable delay before completing.
    ///
    /// PURPOSE:
    /// - Makes distributed execution visible in logs.
    /// - Helps demonstrate worker coordination, pause/resume behavior, and claim ownership.
    /// - Can be used to create enough execution time to observe runtime state transitions.
    ///
    /// CONFIG:
    /// - delayMs: optional artificial delay in milliseconds. Default is 1000.
    /// - message: optional completion message.
    /// </summary>
    [AiStep(DemoStepKeys.Delay)]
    public sealed class DemoDelayStep : IAiStep
    {
        /// <summary>
        /// Gets the registered step name.
        /// </summary>
        public string Name => DemoStepKeys.Delay;

        /// <summary>
        /// Executes the delay demo step.
        /// </summary>
        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var delayMs = await helper.GetConfigAsync<int?>(
                "delayMs",
                cancellationToken).ConfigureAwait(false) ?? 1000;

            if (delayMs < 0)
            {
                throw new InvalidOperationException(
                    "Config value 'delayMs' must be greater than or equal to zero.");
            }

            var message = await helper.GetConfigAsync<string>(
                "message",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(message))
            {
                message = $"Demo delay step completed after {delayMs} ms.";
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            return AiStepResult.Ok(
                output: message,
                data: helper.ToDictionary(new
                {
                    executionId = helper.ExecutionId,
                    stepName = helper.StepName,
                    stepKey = helper.StepKey,
                    stepType = DemoStepKeys.Delay,
                    delayMs,
                    message
                }, ignoreNull: false));
        }
    }
}