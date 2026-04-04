using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

[AiStep("test-flaky-retry2")]
public sealed class TestFlakyRetryStep : IAiStep
{
    private readonly Func<AiStepResult> _onExecute;

    public TestFlakyRetryStep()
    {
        _onExecute = () => throw new InvalidOperationException("Fail");
    }

    public string Key => "test-flaky-retry";

    public string Name => "test-flaky-retry2";

    public Task<AiStepResult> ExecuteAsync(
        AiStepExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_onExecute());
    }
}