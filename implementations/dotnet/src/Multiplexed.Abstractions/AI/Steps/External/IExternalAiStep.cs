namespace Multiplexed.Abstractions.AI.Steps.External
{
    /// <summary>
    /// Non-typed external step contract.
    /// </summary>
    public interface IExternalAiStep
    {
        string StepType { get; }

        Type ExecutionContextType { get; }

        Task<object?> ExecuteUntypedAsync(
            object context,
            CancellationToken cancellationToken);
    }
}