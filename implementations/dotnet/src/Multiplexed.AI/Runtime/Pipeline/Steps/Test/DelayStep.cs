using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Test
{
    /// <summary>
    /// Step that waits for a configurable delay.
    /// Used for timeout and concurrency testing.
    /// </summary>
    [AiStep("delay-step")]
    public sealed class DelayStep : IAiStep
    {
        public string Name => "delay-step";

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var delayMs = await helper.GetConfigAsync<int?>(
                "delayMs",
                cancellationToken).ConfigureAwait(false) ?? 0;

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            return AiStepResult.Ok(
                output: "Delayed execution complete",
                data: helper.ToDictionary(new
                {
                    delayMs
                }));
        }
    }
}