using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

public class FakeStep : IAiStep
{
    private readonly string _output;
    private readonly Dictionary<string, object?> _data;

    public string Name { get; }

    public FakeStep(
        string name,
        string output = "processed",
        Dictionary<string, object?>? data = null)
    {
        Name = name;
        _output = output;
        _data = data ?? new Dictionary<string, object?>
        {
            ["result"] = output
        };
    }

    public Task<AiStepResult> ExecuteAsync(
        AiStepExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            AiStepResult.Ok(
                output: _output,
                data: _data));
    }
}