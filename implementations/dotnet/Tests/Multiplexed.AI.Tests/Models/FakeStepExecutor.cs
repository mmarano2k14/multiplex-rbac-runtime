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

        context.Execution.StateWriter.GetOrCreateStep(context.Execution.State, resolvedStep.Name);

        var result = await resolvedStep.Step.ExecuteAsync(context, ct);

        context.Execution.StateWriter.SetStepResult(
            context.Execution.State,
            resolvedStep.Name,
            result);

        return result;

    }
}