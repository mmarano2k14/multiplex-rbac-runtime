using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Test
{
    /// <summary>
    /// Step that always fails through an unsuccessful result.
    /// Used to validate retry exhaustion deterministically.
    /// </summary>
    [AiStep("always-fail")]
    public sealed class AlwaysFailStep : IAiStep
    {
        public string Name => "always-fail";

        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiStepResult
            {
                Success = false,
                Error = "Intentional permanent failure"
            });
        }
    }
}