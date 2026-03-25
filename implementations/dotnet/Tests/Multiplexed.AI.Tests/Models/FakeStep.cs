using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

public class FakeStep : IAiStep
{
    public string Name { get; }

    public FakeStep(string name)
    {
        Name = name;
    }

    public Task<AiStepResult> ExecuteAsync(
        AiExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            AiStepResult.Ok(
                output: "processed",
                data: new Dictionary<string, object?>
                {
                    ["result"] = "processed"
                }));
    }
}