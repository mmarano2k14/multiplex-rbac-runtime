using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline.Steps
{
    /// <summary>
    /// Example step that produces a hello-world style message.
    ///
    /// Input resolution strategy:
    /// 1. Try declarative step input binding ("text")
    /// 2. Fallback to the legacy shared execution state input ("input")
    ///
    /// This allows the step to work both:
    /// - in declarative pipelines using explicit input binding
    /// - in simpler legacy execution flows where the input is stored directly in state
    /// </summary>
    [AiStep("hello-world")]
    public sealed class HelloWorldStep : IAiStep
    {
        public string Name => "hello-world";

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            // First try the declarative input binding for the current step.
            var text = context.ResolveCurrentStepInput<string>("text");

            // Optional step configuration for test/demo delay simulation.
            if (context.TryGetStepConfigValue<int>("delayMs", out var delayMs) && delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            return AiStepResult.Ok(
                output: "Hello World : " + (text ?? "No text provided"),
                data: new Dictionary<string, object?>
                {
                    ["message"] = "Hello World : " + (text ?? "No text provided")
                });
        }
    }
}