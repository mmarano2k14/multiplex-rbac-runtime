namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue
{
    /// <summary>
    /// Represents an indexed local runtime run and its associated execution.
    /// </summary>
    public sealed class AiRuntimeRunExecutionIndexEntry
    {
        public required string RunId { get; init; }

        public string? ExecutionId { get; init; }

        public string? RuntimeInstanceId { get; init; }

        public string? Status { get; init; }

        public string? FailureReason { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset? StartedAtUtc { get; init; }

        public DateTimeOffset? CompletedAtUtc { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}