using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

public class FakeStepExecutor : IAiStepExecutor
{
    public Task<AiStepResult> ExecuteAsync(
        IAiStep step,
        AiExecutionContext context,
        CancellationToken ct)
    {
        return step.ExecuteAsync(context, ct);
    }
}