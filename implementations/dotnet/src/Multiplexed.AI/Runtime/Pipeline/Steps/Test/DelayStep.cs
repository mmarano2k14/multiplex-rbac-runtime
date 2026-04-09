using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

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

            if (context.TryGetStepConfigValue<int>("delayMs", out var delayMs) && delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            return AiStepResult.Ok(
                output: "Delayed execution complete",
                data: new Dictionary<string, object?>
                {
                    ["delayMs"] = delayMs
                });
        }
    }
}