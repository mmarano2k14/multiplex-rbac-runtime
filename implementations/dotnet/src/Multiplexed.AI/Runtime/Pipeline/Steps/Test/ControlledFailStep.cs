using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Test
{
    /// <summary>
    /// Step that fails based on configuration.
    /// Allows flexible test scenarios.
    /// </summary>
    [AiStep("controlled-fail")]
    public sealed class ControlledFailStep : IAiStep
    {
        public string Name => "controlled-fail";

        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.TryGetStepConfigValue<bool>("shouldFail", out var shouldFail) && shouldFail)
            {
                throw new Exception("Controlled failure");
            }

            return Task.FromResult(
                AiStepResult.Ok(
                    output: "Controlled success",
                    data: new Dictionary<string, object?>
                    {
                        ["controlled"] = true
                    }));
        }
    }
}