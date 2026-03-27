using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.Pipeline.Steps
{
    /// <summary>
    /// Example step that produces a hello-world style message.
    ///
    /// Input resolution strategy:
    /// 1. Try declarative step input binding ("text")
    /// 2. Fallback to shared execution state input ("input")
    ///
    /// This allows the step to work both:
    /// - in declarative pipelines using explicit input binding
    /// - in simpler execution flows where the input is stored directly in state
    /// </summary>
    [AiStep("hello-world")]
    public sealed class HelloWorldStep : IAiStep
    {
        public string Name => "hello-world";

        public Task<AiStepResult> ExecuteAsync(
            AiExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            // First try the step-level declarative input binding.
            var text = context.ResolveInputBinding<string>("text");

            // Fallback to the shared execution state input if no step binding was resolved.
            text ??= context.Get<string>(AiExecutionKeys.Input);

            // Optional step configuration for test/demo delay simulation.
            if (context.TryGetStepConfigValue<int>("delayMs", out var delayMs) && delayMs > 0)
            {
                Thread.Sleep(delayMs);
            }

            return Task.FromResult(
                AiStepResult.Ok(
                    output: "Hello World",
                    data: new Dictionary<string, object?>
                    {
                        ["message"] = "Hello World : " + (text ?? "No text provided")
                    }));
        }
    }
}