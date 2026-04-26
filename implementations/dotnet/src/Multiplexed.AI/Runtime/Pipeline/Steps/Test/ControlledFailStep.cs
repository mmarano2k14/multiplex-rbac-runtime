using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;

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

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var shouldFail = await helper.GetConfigAsync<bool?>(
                "shouldFail",
                cancellationToken).ConfigureAwait(false) ?? false;

            if (shouldFail)
            {
                throw new Exception("Controlled failure");
            }

            return AiStepResult.Ok(
                output: "Controlled success",
                data: helper.ToDictionary(new
                {
                    controlled = true
                }));
        }
    }
}