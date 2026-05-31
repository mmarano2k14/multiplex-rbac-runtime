namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.SharedInstance
{
    /// <summary>
    /// Represents the result of dispatching a shared run to a runtime instance.
    /// </summary>
    public sealed class AiSharedRuntimeInstanceDispatchResult
    {
        /// <summary>
        /// Gets a value indicating whether dispatch succeeded.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the target runtime instance identifier.
        /// </summary>
        public required string RuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the shared run identifier.
        /// </summary>
        public string? SharedRunId { get; init; }

        /// <summary>
        /// Gets the local runtime queue run identifier created by the target runtime instance.
        /// </summary>
        public string? LocalRunId { get; init; }

        /// <summary>
        /// Gets the execution identifier if it is already known.
        /// </summary>
        /// <remarks>
        /// This may be null immediately after local enqueue because the execution id
        /// can be created asynchronously when the local runtime starts the run.
        /// </remarks>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Gets the claim token associated with this dispatch.
        /// </summary>
        public string? ClaimToken { get; init; }

        /// <summary>
        /// Gets a human-readable message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Gets the failure reason when dispatch failed.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// Gets when the dispatch started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; init; }

        /// <summary>
        /// Gets when the dispatch completed.
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; init; }

        /// <summary>
        /// Gets the dispatch duration in milliseconds.
        /// </summary>
        public long DurationMs { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the dispatch result.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}