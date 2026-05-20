namespace Multiplexed.Sample.External.Plugins.Steps.Models
{
    /// <summary>
    /// Represents a serializable result produced by an external demo step.
    /// </summary>
    public sealed record DemoStepResult(
        string StepKey,
        string StepType,
        string Message,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyDictionary<string, object?> Metadata);
}