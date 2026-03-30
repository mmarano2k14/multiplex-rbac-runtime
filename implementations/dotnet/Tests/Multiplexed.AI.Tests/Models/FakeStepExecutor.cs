using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

public class FakeStepExecutor : IAiStepExecutor
{
    public async Task<AiStepResult> ExecuteAsync(
        ResolvedAiPipelineStep resolvedStep,
        AiStepExecutionContext context,
        CancellationToken ct)
    {

        context.State.EnsureStepInitialized(resolvedStep);

        var result = await resolvedStep.Step.ExecuteAsync(context, ct);

        context.State.SetStepResult(resolvedStep.Name, result);

        return result;

    }
}