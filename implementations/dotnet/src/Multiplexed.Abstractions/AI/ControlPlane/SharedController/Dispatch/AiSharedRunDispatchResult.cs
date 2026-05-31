namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController.Dispatch
{
    /// <summary>
    /// Represents the result of dispatching a shared run to a runtime instance.
    /// </summary>
    public sealed class AiSharedRunDispatchResult
    {
        /// <summary>
        /// Indicates whether dispatch succeeded.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Shared controller run identifier.
        /// </summary>
        public required string SharedRunId { get; init; }

        /// <summary>
        /// Target runtime instance id.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Local runtime queue run id returned by the target runtime instance.
        /// </summary>
        public string? LocalRunId { get; init; }

        /// <summary>
        /// Durable DAG execution id when already available.
        /// Usually this is not available immediately at dispatch time.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Optional claim token associated with the dispatched shared queue item.
        /// </summary>
        public string? ClaimToken { get; init; }

        /// <summary>
        /// Optional status message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Optional failure reason when dispatch failed.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// UTC timestamp when dispatch started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; init; }

        /// <summary>
        /// UTC timestamp when dispatch completed.
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; init; }

        /// <summary>
        /// Dispatch duration in milliseconds.
        /// </summary>
        public long DurationMs { get; init; }

        /// <summary>
        /// Optional diagnostics produced by the dispatcher.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; init; } =
            Array.Empty<string>();
    }
}