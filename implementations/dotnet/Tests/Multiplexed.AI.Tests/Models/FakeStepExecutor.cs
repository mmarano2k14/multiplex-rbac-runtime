using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

public class FakeStepExecutor : IAiStepExecutor
{
    public Task<AiStepResult> ExecuteAsync(
        ResolvedAiPipelineStep resolvedStep,
        AiExecutionContext context,
        CancellationToken ct)
    {
        return resolvedStep.Step.ExecuteAsync(context, ct);
    }
}