using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;

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

            var helper = context.GetHelper();

            // First try the declarative input binding for the current step.
            var text = await helper.GetInputAsync<string>(
                "text",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(text))
            {
                text = await helper.GetDataAsync<string>(
                    "input",
                    cancellationToken).ConfigureAwait(false);
            }

            // Optional step configuration for test/demo delay simulation.
            var delayMs = await helper.GetConfigAsync<int?>(
                "delayMs",
                cancellationToken).ConfigureAwait(false) ?? 0;

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            var message = "Hello World : " + (text ?? "No text provided");

            return AiStepResult.Ok(
                output: message,
                data: helper.ToDictionary(new
                {
                    message
                }));
        }
    }
}